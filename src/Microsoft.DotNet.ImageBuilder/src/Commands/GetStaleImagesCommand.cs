// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GetStaleImagesCommand : Command<GetStaleImagesOptions, GetStaleImagesOptionsBuilder>, IDisposable
    {
        private readonly Dictionary<string, string> _imageDigests = new();
        private readonly object _imageDigestsLock = new();
        private readonly IManifestToolService _manifestToolService;
        private readonly ILoggerService _loggerService;
        private readonly IGitHubClientFactory _gitHubClientFactory;
        private readonly HttpClient _httpClient;

        [ImportingConstructor]
        public GetStaleImagesCommand(
            IManifestToolService manifestToolService,
            IHttpClientProvider httpClientProvider,
            ILoggerService loggerService,
            IGitHubClientFactory gitHubClientFactory)
        {
            _manifestToolService = manifestToolService;
            _loggerService = loggerService;
            _gitHubClientFactory = gitHubClientFactory;
            _httpClient = httpClientProvider.GetClient();
        }

        protected override string Description => "Gets paths to images whose base images are out-of-date";

        public override async Task ExecuteAsync()
        {
            if (Options.SubscriptionOptions.SubscriptionsPath is null)
            {
                throw new InvalidOperationException("Subscriptions path must be set.");
            }

            IEnumerable<Task<SubscriptionImagePaths>> getPathResults =
                (await SubscriptionHelper.GetSubscriptionManifestsAsync(
                    Options.SubscriptionOptions.SubscriptionsPath, Options.FilterOptions, _httpClient, _loggerService))
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

        private async Task<IEnumerable<string>> GetPathsToRebuildAsync(Subscription subscription, ManifestInfo manifest)
        {
            ImageArtifactDetails imageArtifactDetails = await GetImageInfoForSubscriptionAsync(subscription, manifest);

            List<string> pathsToRebuild = new();

            IEnumerable<PlatformInfo> allPlatforms = manifest.GetAllPlatforms().ToList();

            foreach (RepoInfo repo in manifest.FilteredRepos)
            {
                IEnumerable<PlatformInfo> platforms = repo.FilteredImages
                    .SelectMany(image => image.FilteredPlatforms)
                    .Where(platform => !platform.IsInternalFromImage(platform.FinalStageFromImage));

                RepoData? repoData = imageArtifactDetails.Repos
                    .FirstOrDefault(s => s.Repo == repo.Name);

                foreach (PlatformInfo platform in platforms)
                {
                    pathsToRebuild.AddRange(GetPathsToRebuild(allPlatforms, platform, repoData));
                }
            }

            return pathsToRebuild.Distinct().ToList();
        }

        private List<string> GetPathsToRebuild(
            IEnumerable<PlatformInfo> allPlatforms, PlatformInfo platform, RepoData? repoData)
        {
            bool foundImageInfo = false;

            List<string> pathsToRebuild = new();

            void processPlatformWithMissingImageInfo(PlatformInfo platform)
            {
                _loggerService.WriteMessage(
                    $"WARNING: Image info not found for '{platform.DockerfilePath}'. Adding path to build to be queued anyway.");
                IEnumerable<PlatformInfo> dependentPlatforms = platform.GetDependencyGraph(allPlatforms);
                pathsToRebuild.AddRange(dependentPlatforms.Select(p => p.Model.Dockerfile));
            }

            if (repoData == null || repoData.Images == null)
            {
                processPlatformWithMissingImageInfo(platform);
                return pathsToRebuild;
            }

            foreach (ImageData imageData in repoData.Images)
            {
                PlatformData? platformData = imageData.Platforms
                    .FirstOrDefault(platformData => platformData.PlatformInfo == platform);
                if (platformData != null)
                {
                    foundImageInfo = true;
                    string fromImage = platform.FinalStageFromImage;
                    string currentDigest;

                    currentDigest = LockHelper.DoubleCheckedLockLookup(_imageDigestsLock, _imageDigests, fromImage,
                        () =>
                        {
                            string digest = _manifestToolService.GetManifestDigestSha(ManifestMediaType.Any, fromImage, Options.IsDryRun);
                            return DockerHelper.GetDigestString(DockerHelper.GetRepo(fromImage), digest);
                        });

                    bool rebuildImage = platformData.BaseImageDigest != currentDigest;

                    _loggerService.WriteMessage(
                        $"Checking base image '{fromImage}' from '{platform.DockerfilePath}'{Environment.NewLine}"
                        + $"\tLast build digest:    {platformData.BaseImageDigest}{Environment.NewLine}"
                        + $"\tCurrent digest:       {currentDigest}{Environment.NewLine}"
                        + $"\tImage is up-to-date:  {!rebuildImage}{Environment.NewLine}");

                    if (rebuildImage)
                    {
                        IEnumerable<PlatformInfo> dependentPlatforms = platform.GetDependencyGraph(allPlatforms);
                        pathsToRebuild.AddRange(dependentPlatforms.Select(p => p.Model.Dockerfile));
                    }

                    break;
                }
            }

            if (!foundImageInfo)
            {
                processPlatformWithMissingImageInfo(platform);
            }

            return pathsToRebuild;
        }

        private async Task<ImageArtifactDetails> GetImageInfoForSubscriptionAsync(Subscription subscription, ManifestInfo manifest)
        {
            string imageDataJson;
            using (IGitHubClient gitHubClient = _gitHubClientFactory.GetClient(Options.GitOptions.ToGitHubAuth(), Options.IsDryRun))
            {
                GitHubProject project = new GitHubProject(subscription.ImageInfo.Repo, subscription.ImageInfo.Owner);
                GitHubBranch branch = new GitHubBranch(subscription.ImageInfo.Branch, project);

                GitFile repo = subscription.Manifest;
                imageDataJson = await gitHubClient.GetGitHubFileContentsAsync(subscription.ImageInfo.Path, branch);
            }

            return ImageInfoHelper.LoadFromContent(imageDataJson, manifest, skipManifestValidation: true);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
#nullable disable
