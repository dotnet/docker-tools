// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GetStaleImagesCommand : Command<GetStaleImagesOptions, GetStaleImagesOptionsBuilder>
    {
        private readonly Dictionary<string, string> _imageDigests = new();
        private readonly SemaphoreSlim _imageDigestsLock = new(1);
        private readonly Lazy<IManifestService> _manifestService;
        private readonly ILoggerService _loggerService;
        private readonly IOctokitClientFactory _octokitClientFactory;
        private readonly IGitService _gitService;

        [ImportingConstructor]
        public GetStaleImagesCommand(
            IManifestServiceFactory manifestServiceFactory,
            ILoggerService loggerService,
            IOctokitClientFactory octokitClientFactory,
            IGitService gitService)
        {
            _loggerService = loggerService;
            _octokitClientFactory = octokitClientFactory;
            _gitService = gitService;

            // Don't worry about authenticating to our own ACR, since we are checking base image digests from public
            // registries instead of our staging location. Registry credentials are needed however to prevent rate
            // limiting on other registries we don't own.
            ArgumentNullException.ThrowIfNull(manifestServiceFactory);
            _manifestService = new Lazy<IManifestService>(() =>
                manifestServiceFactory.Create(ownedAcr: null, Options.CredentialsOptions));
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
                    Options.SubscriptionOptions.SubscriptionsPath, Options.FilterOptions, _gitService)
                .Select(async subscriptionManifest =>
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptionManifest.Subscription.Id,
                        ImagePaths =
                            (await GetPathsToRebuildAsync(subscriptionManifest.Subscription, subscriptionManifest.Manifest))
                            .ToArray()
                    });

            SubscriptionImagePaths[] results = await Task.WhenAll(getPathResults);

            // Filter out any results that don't have any images to rebuild
            results = results
                .Where(result => result.ImagePaths.Any())
                .ToArray();

            string outputString = JsonConvert.SerializeObject(results);

            _loggerService.WriteMessage(
                PipelineHelper.FormatOutputVariable(Options.VariableName, outputString)
                    .Replace("\"", "\\\"")); // Escape all quotes

            string formattedResults = JsonConvert.SerializeObject(results, Formatting.Indented);
            _loggerService.WriteMessage(
                $"Image Paths to be Rebuilt:{Environment.NewLine}{formattedResults}");
        }

        private async Task<IEnumerable<string>> GetPathsToRebuildAsync(Models.Subscription.Subscription subscription, ManifestInfo manifest)
        {
            ImageArtifactDetails imageArtifactDetails = await GetImageInfoForSubscriptionAsync(subscription, manifest);

            List<string> pathsToRebuild = new();

            foreach (RepoInfo repo in manifest.FilteredRepos)
            {
                IEnumerable<PlatformInfo> platforms = repo.FilteredImages
                    .SelectMany(image => image.FilteredPlatforms)
                    .Where(platform => platform.FinalStageFromImage is not null && !platform.IsInternalFromImage(platform.FinalStageFromImage));

                foreach (PlatformInfo platform in platforms)
                {
                    pathsToRebuild.AddRange(await GetPathsToRebuildAsync(manifest, platform, repo, imageArtifactDetails));
                }
            }

            return pathsToRebuild.Distinct().ToList();
        }

        private static IEnumerable<PlatformInfo> GetDescendants(PlatformInfo platform, ManifestInfo manifest) =>
            manifest.GetDescendants(platform, manifest.GetAllPlatforms().ToList(), includeAncestorsOfDescendants: true)
                .Prepend(platform);

        private async Task<List<string>> GetPathsToRebuildAsync(
            ManifestInfo manifest, PlatformInfo platform, RepoInfo repo, ImageArtifactDetails imageArtifactDetails)
        {
            string? fromImage = platform.FinalStageFromImage;
            if (fromImage is null)
            {
                _loggerService.WriteMessage(
                    $"There is no base image for '{platform.DockerfilePath}'. By default, it is considered up-to-date.");
                return [];
            }

            (PlatformData Platform, ImageData Image)? matchingPlatform = ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails);

            if (matchingPlatform is null)
            {
                _loggerService.WriteMessage(
                    $"WARNING: Image info not found for '{platform.DockerfilePath}'. Adding path to build to be queued anyway.");
                IEnumerable<PlatformInfo> dependentPlatforms = GetDescendants(platform, manifest);
                return dependentPlatforms.Select(p => p.Model.Dockerfile).ToList();
            }

            fromImage = Options.BaseImageOverrideOptions.ApplyBaseImageOverride(fromImage);

            string currentDigest = await LockHelper.DoubleCheckedLockLookupAsync(_imageDigestsLock, _imageDigests, fromImage,
                async () =>
                {
                    string digest = await _manifestService.Value.GetManifestDigestShaAsync(fromImage, Options.IsDryRun);
                    return DockerHelper.GetDigestString(DockerHelper.GetRepo(fromImage), digest);
                });

            bool rebuildImage = matchingPlatform.Value.Platform.BaseImageDigest != currentDigest;

            _loggerService.WriteMessage(
                $"Checking base image '{fromImage}' from '{platform.DockerfilePath}'{Environment.NewLine}"
                + $"\tLast build digest:    {matchingPlatform.Value.Platform.BaseImageDigest}{Environment.NewLine}"
                + $"\tCurrent digest:       {currentDigest}{Environment.NewLine}"
                + $"\tImage is up-to-date:  {!rebuildImage}{Environment.NewLine}");

            if (rebuildImage)
            {
                IEnumerable<PlatformInfo> dependentPlatforms = GetDescendants(platform, manifest);
                return dependentPlatforms.Select(p => p.Model.Dockerfile).ToList();
            }

            return [];
        }

        private async Task<ImageArtifactDetails> GetImageInfoForSubscriptionAsync(Models.Subscription.Subscription subscription, ManifestInfo manifest)
        {
            IApiConnection connection = OctokitClientFactory.CreateApiConnection(Options.GitOptions.ToOctokitCredentials());

            ITreesClient treesClient = _octokitClientFactory.CreateTreesClient(connection);
            string fileSha = await treesClient.GetFileShaAsync(
                subscription.ImageInfo.Owner, subscription.ImageInfo.Repo, subscription.ImageInfo.Branch, subscription.ImageInfo.Path);

            IBlobsClient blobsClient = _octokitClientFactory.CreateBlobsClient(connection);
            string imageDataJson = await blobsClient.GetFileContentAsync(subscription.ImageInfo.Owner, subscription.ImageInfo.Repo, fileSha);

            return ImageInfoHelper.LoadFromContent(imageDataJson, manifest, skipManifestValidation: true);
        }
    }
}
#nullable disable
