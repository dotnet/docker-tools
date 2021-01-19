// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GetStaleImagesCommand : Command<GetStaleImagesOptions, GetStaleImagesOptionsBuilder>, IDisposable
    {
        private readonly Dictionary<string, string> _gitRepoIdToPathMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _imageDigests = new Dictionary<string, string>();
        private readonly SemaphoreSlim _gitRepoPathSemaphore = new SemaphoreSlim(1);
        private readonly object _imageDigestsLock = new object();
        private readonly IManifestToolService _manifestToolService;
        private readonly ILoggerService _loggerService;
        private readonly IGitHubClientFactory _gitHubClientFactory;
        private readonly HttpClient _httpClient;

        [ImportingConstructor]
        public GetStaleImagesCommand(
            IManifestToolService manifestToolService,
            IHttpClientProvider httpClientFactory,
            ILoggerService loggerService,
            IGitHubClientFactory gitHubClientFactory)
        {
            _manifestToolService = manifestToolService;
            _loggerService = loggerService;
            _gitHubClientFactory = gitHubClientFactory;
            _httpClient = httpClientFactory.GetClient();
        }

        protected override string Description => "Gets paths to images whose base images are out-of-date";

        public override async Task ExecuteAsync()
        {
            string subscriptionsJson = File.ReadAllText(Options.SubscriptionsPath);
            Subscription[] subscriptions = JsonConvert.DeserializeObject<Subscription[]>(subscriptionsJson);

            try
            {
                SubscriptionImagePaths[] results = await Task.WhenAll(
                    subscriptions.Select(async s => new SubscriptionImagePaths
                    {
                        SubscriptionId = s.Id,
                        ImagePaths = (await GetPathsToRebuildAsync(s)).ToArray()
                    }));

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
            finally
            {
                foreach (string repoPath in _gitRepoIdToPathMapping.Values)
                {
                    // The path to the repo is stored inside a zip extraction folder so be sure to delete that
                    // zip extraction folder, not just the inner repo folder.
                    Directory.Delete(new DirectoryInfo(repoPath).Parent.FullName, true);
                }
            }
        }

        private async Task<IEnumerable<string>> GetPathsToRebuildAsync(Subscription subscription)
        {
            // If the command is filtered with an OS type that does not match the OsType filter of the subscription,
            // then there are no images that need to be inspected.
            string osTypeRegexPattern = ManifestFilter.GetFilterRegexPattern(Options.FilterOptions.OsType);
            if (!string.IsNullOrEmpty(subscription.OsType) &&
                !Regex.IsMatch(subscription.OsType, osTypeRegexPattern, RegexOptions.IgnoreCase))
            {
                return Enumerable.Empty<string>();
            }

            _loggerService.WriteMessage($"Processing subscription:  {subscription.Id}");

            string repoPath = await GetGitRepoPath(subscription);

            TempManifestOptions manifestOptions = new TempManifestOptions(Options.FilterOptions)
            {
                Manifest = Path.Combine(repoPath, subscription.Manifest.Path)
            };

            ManifestInfo manifest = ManifestInfo.Load(manifestOptions);

            ImageArtifactDetails imageArtifactDetails = await GetImageInfoForSubscriptionAsync(subscription, manifest);

            List<string> pathsToRebuild = new List<string>();

            IEnumerable<PlatformInfo> allPlatforms = manifest.GetAllPlatforms().ToList();

            foreach (RepoInfo repo in manifest.FilteredRepos)
            {
                IEnumerable<PlatformInfo> platforms = repo.FilteredImages
                    .SelectMany(image => image.FilteredPlatforms)
                    .Where(platform => !platform.IsInternalFromImage(platform.FinalStageFromImage));

                RepoData repoData = imageArtifactDetails.Repos
                    .FirstOrDefault(s => s.Repo == repo.Name);

                foreach (PlatformInfo platform in platforms)
                {
                    pathsToRebuild.AddRange(GetPathsToRebuild(allPlatforms, platform, repoData));
                }
            }

            return pathsToRebuild.Distinct().ToList();
        }

        private List<string> GetPathsToRebuild(
            IEnumerable<PlatformInfo> allPlatforms, PlatformInfo platform, RepoData repoData)
        {
            bool foundImageInfo = false;

            List<string> pathsToRebuild = new List<string>();

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
                PlatformData platformData = imageData.Platforms
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

        private Task<string> GetGitRepoPath(Subscription sub)
        {
            string uniqueName = $"{sub.Manifest.Owner}-{sub.Manifest.Repo}-{sub.Manifest.Branch}";

            return _gitRepoPathSemaphore.DoubleCheckedLockLookupAsync(_gitRepoIdToPathMapping, uniqueName,
                () => GitHelper.DownloadAndExtractGitRepoArchiveAsync(_httpClient, sub.Manifest));
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _gitRepoPathSemaphore.Dispose();
        }

        private class TempManifestOptions : ManifestOptions, IFilterableOptions
        {
            public TempManifestOptions(ManifestFilterOptions filterOptions)
            {
                FilterOptions = filterOptions;
            }

            public ManifestFilterOptions FilterOptions { get; }
        }
    }
}
