// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetStaleImagesCommand : Command<GetStaleImagesOptions>
    {
        private readonly Dictionary<string, string> _imageDigests = new();
        private readonly SemaphoreSlim _imageDigestsLock = new(1);
        private readonly Lazy<IManifestService> _manifestService;
        private readonly IManifestJsonService _manifestJsonService;
        private readonly ILogger<GetStaleImagesCommand> _logger;
        private readonly IGitService _gitService;
        private readonly IImageInfoService _imageInfoService;

        public GetStaleImagesCommand(
            IManifestServiceFactory manifestServiceFactory,
            IManifestJsonService manifestJsonService,
            ILogger<GetStaleImagesCommand> logger,
            IGitService gitService,
            IImageInfoService imageInfoService)
        {
            _manifestJsonService = manifestJsonService ?? throw new ArgumentNullException(nameof(manifestJsonService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _imageInfoService = imageInfoService ?? throw new ArgumentNullException(nameof(imageInfoService));

            // Don't worry about authenticating to our own ACR, since we are checking base image digests from public
            // registries instead of our staging location. Registry credentials are needed however to prevent rate
            // limiting on other registries we don't own.
            ArgumentNullException.ThrowIfNull(manifestServiceFactory);
            _manifestService = new Lazy<IManifestService>(() =>
                manifestServiceFactory.Create(Options.CredentialsOptions));
        }

        protected override string Description => "Gets paths to images whose base images are out-of-date";

        public override async Task ExecuteAsync()
        {
            if (Options.SubscriptionOptions.SubscriptionsPath is null)
            {
                throw new InvalidOperationException("Subscriptions path must be set.");
            }

            IEnumerable<Task<SubscriptionImagePaths>> getPathResults =
                SubscriptionHelper.GetSubscriptionManifests(
                    Options.SubscriptionOptions.SubscriptionsPath,
                    Options.FilterOptions,
                    _gitService,
                    _manifestJsonService,
                    manifestOptions => manifestOptions.RegistryOverride = Options.RegistryOverride)
                .Select(async subscriptionManifest =>
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptionManifest.Subscription.Id,
                        ImagePaths =
                            (await GetPathsToRebuildAsync(subscriptionManifest.Manifest))
                            .ToArray()
                    });

            SubscriptionImagePaths[] results = await Task.WhenAll(getPathResults);

            // Filter out any results that don't have any images to rebuild
            results = results
                .Where(result => result.ImagePaths.Any())
                .ToArray();

            string outputString = JsonConvert.SerializeObject(results);

            _logger.LogInformation(
                PipelineHelper.FormatOutputVariable(Options.VariableName, outputString)
                    .Replace("\"", "\\\"")); // Escape all quotes

            string formattedResults = JsonConvert.SerializeObject(results, Formatting.Indented);
            _logger.LogInformation(
                $"Image Paths to be Rebuilt:{Environment.NewLine}{formattedResults}");
        }

        private async Task<IEnumerable<string>> GetPathsToRebuildAsync(ManifestInfo manifest)
        {
            ImageArtifactDetails imageArtifactDetails = await PullImageInfoAsync(manifest);

            ImageNameResolverForMatrix imageNameResolver = new(
                Options.BaseImageOverrideOptions,
                manifest,
                repoPrefix: null,
                sourceRepoPrefix: Options.SourceRepoPrefix);

            List<string> pathsToRebuild = new();

            foreach (RepoInfo repo in manifest.FilteredRepos)
            {
                IEnumerable<PlatformInfo> platforms = repo.FilteredImages
                    .SelectMany(image => image.FilteredPlatforms)
                    .Where(platform => platform.FinalStageFromImage is not null && !platform.IsInternalFromImage(platform.FinalStageFromImage));

                foreach (PlatformInfo platform in platforms)
                {
                    pathsToRebuild.AddRange(
                        await GetPathsToRebuildAsync(manifest, platform, repo, imageArtifactDetails, imageNameResolver));
                }
            }

            return pathsToRebuild.Distinct().ToList();
        }

        private static IEnumerable<PlatformInfo> GetDescendants(PlatformInfo platform, ManifestInfo manifest) =>
            manifest.GetDescendants(platform, manifest.GetAllPlatforms().ToList(), includeAncestorsOfDescendants: true)
                .Prepend(platform);

        private async Task<List<string>> GetPathsToRebuildAsync(
            ManifestInfo manifest,
            PlatformInfo platform,
            RepoInfo repo,
            ImageArtifactDetails imageArtifactDetails,
            ImageNameResolverForMatrix imageNameResolver)
        {
            string? fromImage = platform.FinalStageFromImage;
            if (fromImage is null)
            {
                _logger.LogInformation(
                    "Dockerfile {DockerfilePath} has no base image. It is automatically considered up-to-date.",
                    platform.DockerfilePath);

                return [];
            }

            (PlatformData Platform, ImageData Image)? matchingPlatform =
                ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails);

            if (matchingPlatform is null)
            {
                _logger.LogWarning(
                    "Image info not found for '{DockerfilePath}'. It will be queued for rebuild.",
                    platform.DockerfilePath);

                IEnumerable<PlatformInfo> dependentPlatforms = GetDescendants(platform, manifest);
                return dependentPlatforms.Select(p => p.Model.Dockerfile).ToList();
            }

            // Resolve where to actually fetch the digest from. For external base images this
            // points to the mirror location in the staging registry; for internal images it is the
            // original FROM tag. The "public" form is the canonical reference matching what gets
            // recorded in image-info.json and so is the right repo to use in the digest comparison
            // string below.
            string baseImagePullReference = imageNameResolver.GetFromImagePullTag(fromImage);
            string baseImagePublicReference = imageNameResolver.GetFromImagePublicTag(fromImage);

            // Cache the manifest digest by pull reference. The digest is a function of where we
            // actually pull bytes from, so the pull reference is the correct cache key.
            string baseImageManifestDigest =
                await LockHelper.DoubleCheckedLockLookupAsync(
                    semaphore: _imageDigestsLock,
                    dictionary: _imageDigests,
                    key: baseImagePullReference,
                    getValue: () =>
                        // This reaches out to the registry to fetch the digest from the pull
                        // reference. For external images, this fetches from the mirror.
                        _manifestService.Value.GetManifestDigestShaAsync(baseImagePullReference, Options.IsDryRun));

            // Build a digest-pinned reference of the form '<public-repo>@sha256:<hex>' (e.g.
            // 'mcr.microsoft.com/dotnet/runtime@sha256:abc123...'). This must be built per-call
            // from this platform's own public reference — two FROM spellings (e.g. 'almalinux:8'
            // vs 'library/almalinux:8') can share a pull reference but resolve to different
            // public references, so the formed string cannot be cached or shared across platforms.
            // The shape matches what's stored in Platform.BaseImageDigest so the equality check
            // below is meaningful.
            string currentBaseImageDigestReference =
                DockerHelper.GetDigestString(
                    repo: DockerHelper.GetRepo(baseImagePublicReference),
                    sha: baseImageManifestDigest);

            bool shouldRebuildImage = matchingPlatform.Value.Platform.BaseImageDigest != currentBaseImageDigestReference;

            _logger.LogInformation(
                "Dockerfile {DockerfilePath} was last built with base image {BaseImagePublicReference} at digest"
                    + " {LastBuildBaseImageDigestReference}. Image {BaseImagePullReference} has current digest"
                    + " {CurrentBaseImageDigestReference}. Up to date: {IsUpToDate}.",
                platform.DockerfilePath,
                baseImagePublicReference,
                matchingPlatform.Value.Platform.BaseImageDigest,
                baseImagePullReference,
                currentBaseImageDigestReference,
                !shouldRebuildImage);

            if (shouldRebuildImage)
            {
                IEnumerable<PlatformInfo> dependentPlatforms = GetDescendants(platform, manifest);
                return dependentPlatforms.Select(p => p.Model.Dockerfile).ToList();
            }

            return [];
        }

        private async Task<ImageArtifactDetails> PullImageInfoAsync(ManifestInfo manifest)
        {
            string imageInfoRegistry = Options.ImageInfoRegistryOverride ?? manifest.Model.Registry;
            if (string.IsNullOrWhiteSpace(imageInfoRegistry))
            {
                throw new InvalidOperationException(
                    $"Manifest '{manifest.FilePath}' must define a registry or --image-info-registry-override must be set " +
                    "to pull image-info artifact for stale image detection.");
            }

            string imageInfoContent = await _imageInfoService.PullImageInfoArtifactAsync(
                manifest,
                imageInfoRegistry,
                Options.ImageInfoRepoPrefix);

            return ImageInfoHelper.LoadFromContent(imageInfoContent, manifest, skipManifestValidation: true);
        }
    }
}
