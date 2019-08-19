// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GetStaleImagesCommand : Command<GetStaleImagesOptions>, IDisposable
    {
        private readonly Dictionary<string, string> gitRepoIdToPathMapping = new Dictionary<string, string>();
        private readonly Dictionary<string, string> imageDigests = new Dictionary<string, string>();
        private readonly SemaphoreSlim gitRepoPathSemaphore = new SemaphoreSlim(1);
        private readonly SemaphoreSlim imageDigestsSemaphore = new SemaphoreSlim(1);
        private readonly IDockerService dockerService;
        private readonly ILoggerService loggerService;
        private readonly HttpClient httpClient;

        [ImportingConstructor]
        public GetStaleImagesCommand(
            IDockerService dockerService,
            IHttpClientFactory httpClientFactory,
            ILoggerService loggerService)
        {
            this.dockerService = dockerService;
            this.loggerService = loggerService;
            this.httpClient = httpClientFactory.GetClient();
        }

        public override async Task ExecuteAsync()
        {
            string subscriptionsJson = File.ReadAllText(Options.SubscriptionsPath);
            Subscription[] subscriptions = JsonConvert.DeserializeObject<Subscription[]>(subscriptionsJson);

            string imageDataJson = File.ReadAllText(Options.ImageInfoPath);
            RepoData[] repos = JsonConvert.DeserializeObject<RepoData[]>(imageDataJson);

            try
            {
                var results = await Task.WhenAll(
                    subscriptions.Select(async s => new SubscriptionImagePaths
                    {
                        SubscriptionId = s.Id,
                        ImagePaths = (await GetPathsToRebuildAsync(s, repos)).ToArray()
                    }));

                string outputString = JsonConvert.SerializeObject(results);

                this.loggerService.WriteMessage(
                    PipelineHelper.FormatOutputVariable(Options.VariableName, outputString)
                        .Replace("\"", "\\\"")); // Escape all quotes
            }
            finally
            {
                foreach (string repoPath in gitRepoIdToPathMapping.Values)
                {
                    // The path to the repo is stored inside a zip extraction folder so be sure to delete that
                    // zip extraction folder, not just the inner repo folder.
                    Directory.Delete(new DirectoryInfo(repoPath).Parent.FullName, true);
                }
            }
        }

        private async Task<IEnumerable<string>> GetPathsToRebuildAsync(Subscription subscription, RepoData[] repos)
        {
            string repoPath = await GetGitRepoPath(subscription);

            TempManifestOptions manifestOptions = new TempManifestOptions(Options.FilterOptions)
            {
                Manifest = Path.Combine(repoPath, subscription.ManifestPath)
            };

            ManifestInfo manifest = ManifestInfo.Load(manifestOptions);

            List<string> pathsToRebuild = new List<string>();

            IEnumerable<PlatformInfo> allPlatforms = manifest.GetAllPlatforms().ToList();

            foreach (RepoInfo repo in manifest.FilteredRepos)
            {
                IEnumerable<PlatformInfo> platforms = repo.FilteredImages
                    .SelectMany(image => image.FilteredPlatforms);

                RepoData repoData = repos
                    .FirstOrDefault(s => s.Repo == repo.Model.Name);

                
                foreach (var platform in platforms)
                {
                    if (repoData != null &&
                        repoData.Images != null &&
                        repoData.Images.TryGetValue(platform.BuildContextPath, out ImageData imageData))
                    {
                        bool hasDigestChanged = false;
                        
                        foreach (string fromImage in platform.ExternalFromImages)
                        {
                            string currentDigest;

                            await this.imageDigestsSemaphore.WaitAsync();
                            try
                            {
                                if (!this.imageDigests.TryGetValue(fromImage, out currentDigest))
                                {
                                    this.dockerService.PullImage(fromImage, Options.IsDryRun);
                                    currentDigest = this.dockerService.GetImageDigest(fromImage, Options.IsDryRun);
                                    this.imageDigests.Add(fromImage, currentDigest);
                                }
                            }
                            finally
                            {
                                this.imageDigestsSemaphore.Release();
                            }
                            
                            string lastDigest = imageData.BaseImages?[fromImage];

                            if (lastDigest != currentDigest)
                            {
                                hasDigestChanged = true;
                                break;
                            }
                        }

                        if (hasDigestChanged)
                        {
                            IEnumerable<PlatformInfo> dependentPlatforms = platform.GetDependencyGraph(allPlatforms);
                            pathsToRebuild.AddRange(dependentPlatforms.Select(p => p.BuildContextPath));
                        }
                    }
                    else
                    {
                        this.loggerService.WriteMessage($"WARNING: Image info not found for '{platform.BuildContextPath}'. Adding path to build to be queued anyway.");
                        pathsToRebuild.Add(platform.BuildContextPath);
                    }
                }
            }

            return pathsToRebuild.Distinct().ToList();
        }

        private async Task<string> GetGitRepoPath(Subscription sub)
        {
            string repoPath;
            await gitRepoPathSemaphore.WaitAsync();
            try
            {
                string uniqueName = $"{sub.RepoInfo.Owner}-{sub.RepoInfo.Name}-{sub.RepoInfo.Branch}";
                if (!this.gitRepoIdToPathMapping.TryGetValue(uniqueName, out repoPath))
                {
                    string extractPath = Path.Combine(Path.GetTempPath(), uniqueName);
                    Uri repoContentsUrl = GitHelper.GetArchiveUrl(sub.RepoInfo);
                    string zipPath = Path.Combine(Path.GetTempPath(), $"{uniqueName}.zip");
                    File.WriteAllBytes(zipPath, await this.httpClient.GetByteArrayAsync(repoContentsUrl));

                    try
                    {
                        ZipFile.ExtractToDirectory(zipPath, extractPath);
                    }
                    finally
                    {
                        File.Delete(zipPath);
                    }

                    repoPath = Path.Combine(extractPath, $"{sub.RepoInfo.Name}-{sub.RepoInfo.Branch}");
                    this.gitRepoIdToPathMapping.Add(uniqueName, repoPath);
                }
            }
            finally
            {
                gitRepoPathSemaphore.Release();
            }

            return repoPath;
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
            this.gitRepoPathSemaphore.Dispose();
            this.imageDigestsSemaphore.Dispose();
        }

        private class TempManifestOptions : ManifestOptions, IFilterableOptions
        {
            public TempManifestOptions(ManifestFilterOptions filterOptions)
            {
                FilterOptions = filterOptions;
            }

            public ManifestFilterOptions FilterOptions { get; }

            protected override string CommandHelp => throw new NotImplementedException();
        }
    }
}
