// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishMcrReadmesCommand : Command<PublishMcrReadmesOptions>
    {
        private const int MaxTries = 10;
        private const int RetryMillisecondsDelay = 5000;

        public PublishMcrReadmesCommand() : base()
        {
        }

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("PUBLISHING READMES TO MCR");

            // Hookup a TraceListener in order to capture details from Microsoft.DotNet.VersionTools
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            string productRepo = GetProductRepo();

            GitHubAuth githubAuth = new GitHubAuth(Options.GitAuthToken, Options.GitUsername, Options.GitEmail);
            using (GitHubClient client = new GitHubClient(githubAuth))
            {
                for (int i = 0; i < MaxTries; i++)
                {
                    try
                    {
                        GitHubProject project = new GitHubProject(Options.GitRepo, Options.GitOwner);
                        GitHubBranch branch = new GitHubBranch(Options.GitBranch, project);
                        GitObject[] gitObjects = await GetUpdatedReadMes(productRepo, client, branch);

                        if (gitObjects.Any())
                        {
                            string masterRef = $"heads/{Options.GitBranch}";
                            GitReference currentMaster = await client.GetReferenceAsync(project, masterRef);
                            string masterSha = currentMaster.Object.Sha;
                            GitTree tree = await client.PostTreeAsync(project, masterSha, gitObjects);
                            string commitMessage = $"Mirroring {productRepo} readmes";
                            GitCommit commit = await client.PostCommitAsync(
                                project, commitMessage, tree.Sha, new[] { masterSha });

                            // Only fast-forward. Don't overwrite other changes: throw exception instead.
                            await client.PatchReferenceAsync(project, masterRef, commit.Sha, force: false);
                        }

                        break;
                    }
                    catch (HttpRequestException ex) when (i < (MaxTries - 1))
                    {
                        Logger.WriteMessage($"Encountered exception publishing readmes: {ex.Message}");
                        Logger.WriteMessage($"Trying again in {RetryMillisecondsDelay}ms. {MaxTries - i - 1} tries left.");
                        await Task.Delay(RetryMillisecondsDelay);
                    }
                }
            }
        }

        private string GetProductRepo()
        {
            string firstRepoName = Manifest.AllRepos.First().Name
                .TrimStart($"{Manifest.Registry}/");
            return firstRepoName.Substring(0, firstRepoName.LastIndexOf('/'));
        }

        private async Task<GitObject[]> GetUpdatedReadMes(string productRepo, GitHubClient client, GitHubBranch branch)
        {
            List<GitObject> versionInfo = new List<GitObject>();

            List<string> readmePaths = Manifest.FilteredRepos
                .Select(repo => repo.Model.ReadmePath)
                .ToList();

            if (!string.IsNullOrEmpty(Manifest.Model.ReadmePath))
            {
                readmePaths.Add(Manifest.Model.ReadmePath);
            }

            foreach (string readmePath in readmePaths)
            {
                string currentReadMe = File.ReadAllText(readmePath);
                string gitReadmePath = string.Join('/', Options.GitPath, productRepo, readmePath);
                string lastReadMe = await client.GetGitHubFileContentsAsync(gitReadmePath, branch);

                if (lastReadMe == currentReadMe)
                {
                    Logger.WriteMessage($"Readme has not changed:  {readmePath}");
                }
                else
                {
                    Logger.WriteMessage($"Readme has changed:  {readmePath}");
                    versionInfo.Add(new GitObject
                    {
                        Path = gitReadmePath,
                        Type = GitObject.TypeBlob,
                        Mode = GitObject.ModeFile,
                        Content = currentReadMe
                    });
                }
            }

            return versionInfo.ToArray();
        }
    }
}
