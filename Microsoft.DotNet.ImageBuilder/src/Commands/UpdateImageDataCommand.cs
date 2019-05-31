// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ImageModel;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateImageDataCommand : Command<UpdateImageDataOptions>
    {
        public override async Task ExecuteAsync()
        {
            List<RepoData[]> srcReposList = new List<RepoData[]>();
            
            foreach (string sourceImageDataPath in Directory.EnumerateFiles(Options.SourceImageDataFolderPath, "*.json", SearchOption.AllDirectories))
            {
                string srcImageDataContents = File.ReadAllText(sourceImageDataPath);
                RepoData[] srcRepos = JsonConvert.DeserializeObject<RepoData[]>(srcImageDataContents);
                srcReposList.Add(srcRepos);
            }

            GitHubAuth githubAuth = new GitHubAuth(Options.GitAuthToken, Options.GitUsername, Options.GitEmail);
            using (GitHubClient client = new GitHubClient(githubAuth))
            {
                GitHubProject project = new GitHubProject(Options.GitRepo, Options.GitOwner);
                GitHubBranch branch = new GitHubBranch(Options.GitBranch, project);

                string originalTargetImageDataContents = await client.GetGitHubFileContentsAsync(Options.GitImageDataPath, branch);
                List<RepoData> targetRepos = JsonConvert.DeserializeObject<RepoData[]>(originalTargetImageDataContents).ToList();

                foreach (RepoData[] srcRepos in srcReposList)
                {
                    MergeRepos(srcRepos, targetRepos);
                }

                targetRepos = RepoData.SortRepoData(targetRepos);

                string newTargetImageDataContents = JsonHelper.SerializeObject(targetRepos.ToArray()) + Environment.NewLine;

                if (originalTargetImageDataContents != newTargetImageDataContents)
                {
                    GitObject imageDataGitObject = new GitObject
                    {
                        Path = Options.GitImageDataPath,
                        Type = GitObject.TypeBlob,
                        Mode = GitObject.ModeFile,
                        Content = newTargetImageDataContents
                    };

                    string masterRef = $"heads/{Options.GitBranch}";
                    GitReference currentMaster = await client.GetReferenceAsync(project, masterRef);
                    string masterSha = currentMaster.Object.Sha;
                    GitTree tree = await client.PostTreeAsync(project, masterSha, new GitObject[] { imageDataGitObject });

                    GitCommit commit = await client.PostCommitAsync(project, "Merging image data updates from build.", tree.Sha, new[] { masterSha });

                    // Only fast-forward. Don't overwrite other changes: throw exception instead.
                    await client.PatchReferenceAsync(project, masterRef, commit.Sha, force: false);
                }
                else
                {
                    Logger.WriteMessage("No image data differences were found.");
                }
            }
        }

        private void MergeRepos(RepoData[] srcRepos, List<RepoData> targetRepos)
        {
            foreach (RepoData srcRepo in srcRepos)
            {
                RepoData targetRepo = targetRepos.FirstOrDefault(r => r.Repo == srcRepo.Repo);
                if (targetRepo == null)
                {
                    targetRepos.Add(srcRepo);
                }
                else
                {
                    MergeImages(srcRepo, targetRepo);
                }
            }
        }

        private void MergeImages(RepoData srcRepo, RepoData targetRepo)
        {
            if (srcRepo.Images == null)
            {
                return;
            }

            if (srcRepo.Images.Any() && targetRepo.Images == null)
            {
                targetRepo.Images = new Dictionary<string, ImageData>();
            }

            foreach (KeyValuePair<string, ImageData> srcKvp in srcRepo.Images)
            {
                if (targetRepo.Images.TryGetValue(srcKvp.Key, out ImageData targetImage))
                {
                    MergeDigests(srcKvp.Value, targetImage);
                }
                else
                {
                    targetRepo.Images.Add(srcKvp.Key, srcKvp.Value);
                }
            }
        }

        private void MergeDigests(ImageData srcImage, ImageData targetImage)
        {
            if (srcImage.BaseImageDigests == null)
            {
                return;
            }

            if (srcImage.BaseImageDigests.Any() && targetImage == null)
            {
                targetImage.BaseImageDigests = new Dictionary<string, string>();
            }

            foreach (KeyValuePair<string, string> srcKvp in srcImage.BaseImageDigests)
            {
                targetImage.BaseImageDigests[srcKvp.Key] = srcKvp.Value;
            }
        }
    }
}
