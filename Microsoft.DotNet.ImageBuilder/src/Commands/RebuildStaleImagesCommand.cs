// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class RebuildStaleImagesCommand : Command<RebuildStaleImagesOptions>
    {
        private Dictionary<string, string> gitRepoIdToPathMapping = new Dictionary<string, string>();
        private SemaphoreSlim gitRepoPathSemaphore = new SemaphoreSlim(1);

        public override async Task ExecuteAsync()
        {
            string subscriptionsJson = File.ReadAllText(Options.SubscriptionsPath);
            Subscription[] subscriptions = JsonConvert.DeserializeObject<Subscription[]>(subscriptionsJson);

            string imageDataJson = File.ReadAllText(Options.ImageInfoPath);
            RepoData[] repos = JsonConvert.DeserializeObject<RepoData[]>(imageDataJson);

            try
            {
                await Task.WhenAll(subscriptions.Select(s => QueueBuildForStaleImages(s, repos)));
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

        private async Task QueueBuildForStaleImages(Subscription subscription, RepoData[] repos)
        {
            IEnumerable<string> pathsToRebuild = await GetPathsToRebuildAsync(subscription, repos);

            if (!pathsToRebuild.Any())
            {
                Logger.WriteMessage($"All images for subscription '{subscription}' are using up-to-date base images. No rebuild necessary.");
                return;
            }

            string formattedParameters = pathsToRebuild
                .Select(path => $"{ManifestFilterOptions.FormattedPathOption} '{path}'")
                .Aggregate((p1, p2) => $"{p1} {p2}");

            string parameters = "{\"" + subscription.PipelineTrigger.PathVariable + "\": \"" + formattedParameters + "\"}";

            Logger.WriteMessage($"Queueing build for subscription {subscription} with parameters {parameters}.");

            if (Options.IsDryRun)
            {
                return;
            }

            using (VssConnection connection = new VssConnection(
                new Uri($"https://dev.azure.com/{Options.BuildOrganization}"),
                new VssBasicCredential(String.Empty, Options.BuildPersonalAccessToken)))
            using (ProjectHttpClient projectHttpClient = connection.GetClient<ProjectHttpClient>())
            using (BuildHttpClient client = connection.GetClient<BuildHttpClient>())
            {
                TeamProject project = await projectHttpClient.GetProject(Options.BuildProject);

                Build build = new Build
                {
                    Project = new TeamProjectReference { Id = project.Id },
                    Definition = new BuildDefinitionReference { Id = subscription.PipelineTrigger.Id },
                    SourceBranch = subscription.RepoInfo.Branch,
                    Parameters = parameters
                };

                if (await HasInProgressBuildAsync(client, subscription.PipelineTrigger.Id, project.Id))
                {
                    Logger.WriteMessage(
                        $"An in-progress build was detected on the pipeline for subscription '{subscription.ToString()}'. Queueing the build will be skipped.");
                    return;
                }

                await client.QueueBuildAsync(build);
            }
        }

        private async Task<bool> HasInProgressBuildAsync(BuildHttpClient client, int pipelineId, Guid projectId)
        {
            IPagedList<Build> builds = await client.GetBuildsAsync2(
                projectId, definitions: new int[] { pipelineId }, statusFilter: BuildStatus.InProgress);
            return builds.Any();
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
                            DockerHelper.PullImage(fromImage, Options.IsDryRun);
                            string currentDigest = DockerHelper.GetImageDigest(fromImage, Options.IsDryRun);
                            string lastDigest = imageData.BaseImages?[fromImage];

                            if (lastDigest != currentDigest)
                            {
                                hasDigestChanged = true;
                                break;
                            }
                        }

                        if (hasDigestChanged)
                        {
                            pathsToRebuild.Add(platform.BuildContextPath);
                        }
                    }
                    else
                    {
                        Logger.WriteMessage($"WARNING: Image info not found for '{platform.BuildContextPath}'. Adding path to build to be queued anyway.");
                        pathsToRebuild.Add(platform.BuildContextPath);
                    }
                }
            }

            return pathsToRebuild;
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
                    using (HttpClient client = new HttpClient())
                    {
                        string extractPath = Path.Combine(Path.GetTempPath(), uniqueName);
                        string repoContentsUrl =
                            $"https://www.github.com/{sub.RepoInfo.Owner}/{sub.RepoInfo.Name}/archive/{sub.RepoInfo.Branch}.zip";
                        string zipPath = Path.Combine(Path.GetTempPath(), $"{uniqueName}.zip");
                        File.WriteAllBytes(zipPath, await client.GetByteArrayAsync(repoContentsUrl));

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
            }
            finally
            {
                gitRepoPathSemaphore.Release();
            }

            return repoPath;
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
