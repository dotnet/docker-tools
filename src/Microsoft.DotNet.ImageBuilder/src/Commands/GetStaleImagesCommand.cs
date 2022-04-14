// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GetStaleImagesCommand : Command<GetStaleImagesOptions, GetStaleImagesOptionsBuilder>, IDisposable
    {
        private readonly Dictionary<string, string> _imageDigests = new();
        private readonly SemaphoreSlim _imageDigestsLock = new(1);
        private readonly IManifestToolService _manifestToolService;
        private readonly ILoggerService _loggerService;
        private readonly IOctokitClientFactory _octokitClientFactory;
        private readonly HttpClient _httpClient;

        [ImportingConstructor]
        public GetStaleImagesCommand(
            IManifestToolService manifestToolService,
            IHttpClientProvider httpClientProvider,
            ILoggerService loggerService,
            IOctokitClientFactory octokitClientFactory)
        {
            _manifestToolService = manifestToolService;
            _loggerService = loggerService;
            _octokitClientFactory = octokitClientFactory;
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

        private async Task<IEnumerable<string>> GetPathsToRebuildAsync(Models.Subscription.Subscription subscription, ManifestInfo manifest)
        {
            ImageArtifactDetails imageArtifactDetails = await GetImageInfoForSubscriptionAsync(subscription, manifest);

            List<string> pathsToRebuild = new();

            IEnumerable<PlatformInfo> allPlatforms = manifest.GetAllPlatforms().ToList();

            foreach (RepoInfo repo in manifest.FilteredRepos)
            {
                IEnumerable<PlatformInfo> platforms = repo.FilteredImages
                    .SelectMany(image => image.FilteredPlatforms)
                    .Where(platform => platform.FinalStageFromImage is not null && !platform.IsInternalFromImage(platform.FinalStageFromImage));

                RepoData? repoData = imageArtifactDetails.Repos
                    .FirstOrDefault(s => s.Repo == repo.Name);

                foreach (PlatformInfo platform in platforms)
                {
                    pathsToRebuild.AddRange(await GetPathsToRebuildAsync(allPlatforms, platform, repoData));
                }
            }

            return pathsToRebuild.Distinct().ToList();
        }

        private async Task<List<string>> GetPathsToRebuildAsync(
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
                    string? fromImage = platform.FinalStageFromImage;
                    string currentDigest;

                    if (fromImage is null)
                    {
                        _loggerService.WriteMessage(
                            $"There is no base image for '{platform.DockerfilePath}'. By default, it is considered up-to-date.");
                        break;
                    }

                    currentDigest = await LockHelper.DoubleCheckedLockLookupAsync(_imageDigestsLock, _imageDigests, fromImage,
                        async () =>
                        {
                            string digest = await _manifestToolService.GetManifestDigestShaAsync(ManifestMediaType.Any, fromImage, Options.IsDryRun);
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

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
#nullable disable
