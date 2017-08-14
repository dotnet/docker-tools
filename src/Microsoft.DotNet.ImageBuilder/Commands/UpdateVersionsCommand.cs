// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateVersionsCommand : Command<UpdateVersionsOptions>
    {
        private const int MaxTries = 10;
        private const int RetryMillisecondsDelay = 5000;

        public UpdateVersionsCommand() : base()
        {
        }

        public override async Task ExecuteAsync()
        {
            Utilities.WriteHeading("UPDATING VERSIONS");

            // Hookup a TraceListener in order to capture details from Microsoft.DotNet.VersionTools
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            DockerHelper.PullBaseImages(Manifest, Options);

            GitHubAuth githubAuth = new GitHubAuth(Options.GitAuthToken, Options.GitUsername, Options.GitEmail);
            using (GitHubClient client = new GitHubClient(githubAuth))
            {
                for (int i = 0; i < MaxTries; i++)
                {
                    try
                    {
                        GitHubProject project = new GitHubProject(Options.GitRepo, Options.GitOwner);
                        GitHubBranch branch = new GitHubBranch(Options.GitBranch, project);
                        GitObject[] gitObjects = await GetUpdatedVerionInfo(client, branch);

                        if (gitObjects.Any())
                        {
                            string masterRef = $"heads/{Options.GitBranch}";
                            GitReference currentMaster = await client.GetReferenceAsync(project, masterRef);
                            string masterSha = currentMaster.Object.Sha;
                            GitTree tree = await client.PostTreeAsync(project, masterSha, gitObjects);
                            string commitMessage = "Update Docker image digests";
                            GitCommit commit = await client.PostCommitAsync(
                                project, commitMessage, tree.Sha, new[] { masterSha });

                            // Only fast-forward. Don't overwrite other changes: throw exception instead.
                            await client.PatchReferenceAsync(project, masterRef, commit.Sha, force: false);
                        }

                        break;
                    }
                    catch (HttpRequestException ex) when (i < (MaxTries - 1))
                    {
                        Console.WriteLine($"Encountered exception committing build-info update: {ex.Message}");
                        Console.WriteLine($"Trying again in {RetryMillisecondsDelay}ms. {MaxTries - i - 1} tries left.");
                        await Task.Delay(RetryMillisecondsDelay);
                    }
                }
            }
        }

        private async Task<GitObject[]> GetUpdatedVerionInfo(GitHubClient client, GitHubBranch branch)
        {
            List<GitObject> versionInfo = new List<GitObject>();

            foreach (string fromImage in Manifest.GetExternalFromImages())
            {
                string currentDigest = DockerHelper.GetImageDigest(fromImage, Options.IsDryRun);
                string versionFile = $"{Options.GitPath}/{fromImage.Replace(':', '/')}.txt";
                string lastDigest = await client.GetGitHubFileContentsAsync(versionFile, branch);

                if (lastDigest == currentDigest)
                {
                    Console.WriteLine($"Image has not changed:  {fromImage}");
                    continue;
                }

                Console.WriteLine($"Image has changed:  {fromImage}");
                Console.WriteLine($"Updating `{versionFile}` with `{currentDigest}`");

                versionInfo.Add(new GitObject
                {
                    Path = versionFile,
                    Type = GitObject.TypeBlob,
                    Mode = GitObject.ModeFile,
                    Content = currentDigest
                });
            }

            return versionInfo.ToArray();
        }
    }
}
