// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ImageModel;
using Microsoft.DotNet.ImageBuilder.SubscriptionsModel;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

            string imageDataJson = File.ReadAllText(Options.ImageDataPath);
            RepoData[] repos = JsonConvert.DeserializeObject<RepoData[]>(imageDataJson);

            try
            {
                await Task.WhenAll(subscriptions.Select(s => ExecuteBuildForStaleImages(s, repos)));
            }
            finally
            {
                foreach (string repoPath in gitRepoIdToPathMapping.Values)
                {
                    Directory.Delete(repoPath, true);
                }
            }
        }

        private async Task ExecuteBuildForStaleImages(Subscription subscription, RepoData[] repos)
        {
            while (true)
            {
                IEnumerable<string> pathsToRebuild = await GetPathsToRebuildAsync(subscription, repos);

                string pathsStr = String.Join(", ", pathsToRebuild.ToArray());
                Logger.WriteMessage(
                    $"The following images in subscription '{subscription.ToString()}' were determined to be using out-of-date base images: {pathsStr}");

                bool wasBuildQueued = await ExecuteBuild(subscription, pathsToRebuild);

                // The build may not have been queued if it needed to wait for an already running build. In that case, that other build may
                // have updated the image data file so we should re-check that file to get the latest digest values to determine what still
                // needs to get rebuilt.
                if (wasBuildQueued)
                {
                    return;
                }

                Logger.WriteMessage("Rechecking digests to determine which images need to be rebuilt.");
            }
        }

        private async Task<bool> ExecuteBuild(Subscription subscription, IEnumerable<string> pathsToRebuild)
        {
            string formattedParameters = String.Join(" ", pathsToRebuild
                .Select(path => $"--path '{path}'")
                .ToArray());

            string parameters = "{\"" + subscription.PipelineTrigger.PathArgsName + "\": \"" + formattedParameters + "\"}";

            using (VssConnection connection = new VssConnection(
                new Uri($"https://dev.azure.com/{Options.BuildOrganization}"),
                new VssBasicCredential(String.Empty, Options.BuildPersonalAccessToken)))
            {
                using (ProjectHttpClient projectHttpClient = connection.GetClient<ProjectHttpClient>())
                {
                    TeamProject project = await projectHttpClient.GetProject(Options.BuildProject);

                    Build build = new Build
                    {
                        Project = new TeamProjectReference { Id = project.Id },
                        Definition = new BuildDefinitionReference { Id = subscription.PipelineTrigger.Id },
                        SourceBranch = subscription.RepoInfo.Branch,
                        Parameters = parameters
                    };

                    using (BuildHttpClient client = connection.GetClient<BuildHttpClient>())
                    {
                        bool waitedForBuild = false;
                        while (await HasInProgressBuildAsync(client, subscription.PipelineTrigger.Id, project.Id))
                        {
                            Logger.WriteMessage(
                                $"An in-progress build was detected on the pipeline for subscription '{subscription.ToString()}'. Waiting until it is finished.");
                            await Task.Delay(TimeSpan.FromMinutes(1));
                            waitedForBuild = true;
                        }

                        if (waitedForBuild)
                        {
                            return false;
                        }

                        await client.QueueBuildAsync(build);
                        return true;
                    }
                }
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

            ManifestInfo manifest = ManifestInfo.Create(
                Path.Combine(repoPath, subscription.ManifestPath),
                new ManifestFilter(),
                new NullManifestOptions());

            List<string> pathsToRebuild = new List<string>();

            foreach (RepoInfo repo in manifest.AllRepos)
            {
                IEnumerable<PlatformInfo> platforms = repo.AllImages
                    .SelectMany(image => image.AllPlatforms);

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
                            string lastDigest = imageData.BaseImageDigests?[fromImage];

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
                        string repoContentsUrl =
                            $"https://www.github.com/{sub.RepoInfo.Owner}/{sub.RepoInfo.Name}/archive/{sub.RepoInfo.Branch}.zip";
                        string zipPath = Path.Combine(Path.GetTempPath(), $"{uniqueName}.zip");
                        File.WriteAllBytes(zipPath, await client.GetByteArrayAsync(repoContentsUrl));

                        string extractPath = Path.Combine(Path.GetTempPath(), uniqueName);
                        ZipFile.ExtractToDirectory(zipPath, extractPath);
                        File.Delete(zipPath);

                        repoPath = Path.Combine(extractPath, $"{sub.RepoInfo.Name}-{sub.RepoInfo.Branch}");
                    }
                }
            }
            finally
            {
                gitRepoPathSemaphore.Release();
            }

            return repoPath;
        }

        private class NullManifestOptions : ManifestOptions
        {
            protected override string CommandHelp => throw new NotImplementedException();
        }
    }
}
