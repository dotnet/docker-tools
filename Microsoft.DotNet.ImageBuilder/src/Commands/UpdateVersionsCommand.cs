// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateVersionsCommand : ManifestCommand<UpdateVersionsOptions>
    {
        public UpdateVersionsCommand() : base()
        {
        }

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("UPDATING VERSIONS");

            // Hookup a TraceListener in order to capture details from Microsoft.DotNet.VersionTools
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            DockerHelper.PullBaseImages(Manifest, Options);

            await GitHelper.ExecuteGitOperationsWithRetryAsync(Options.GitOptions, async client =>
            {
                GitHubProject project = new GitHubProject(Options.GitOptions.Repo, Options.GitOptions.Owner);
                GitHubBranch branch = new GitHubBranch(Options.GitOptions.Branch, project);
                GitObject[] gitObjects = await GetUpdatedVerionInfo(client, branch);

                if (gitObjects.Any())
                {
                    string masterRef = $"heads/{Options.GitOptions.Branch}";
                    GitReference currentMaster = await client.GetReferenceAsync(project, masterRef);
                    string masterSha = currentMaster.Object.Sha;
                    GitTree tree = await client.PostTreeAsync(project, masterSha, gitObjects);
                    string commitMessage = "Update Docker image digests";
                    GitCommit commit = await client.PostCommitAsync(
                        project, commitMessage, tree.Sha, new[] { masterSha });

                    // Only fast-forward. Don't overwrite other changes: throw exception instead.
                    await client.PatchReferenceAsync(project, masterRef, commit.Sha, force: false);
                }
            });
        }

        private async Task<GitObject[]> GetUpdatedVerionInfo(GitHubClient client, GitHubBranch branch)
        {
            List<GitObject> versionInfo = new List<GitObject>();

            foreach (string fromImage in Manifest.GetExternalFromImages())
            {
                string currentDigest = DockerHelper.GetImageDigest(fromImage, Options.IsDryRun);
                string versionFile = $"{Options.GitOptions.Path}/{fromImage.Replace(':', '/')}.txt";
                string lastDigest = await client.GetGitHubFileContentsAsync(versionFile, branch);

                if (lastDigest == currentDigest)
                {
                    Logger.WriteMessage($"Image has not changed:  {fromImage}");
                    continue;
                }

                Logger.WriteMessage($"Image has changed:  {fromImage}");
                Logger.WriteMessage($"Updating `{versionFile}` with `{currentDigest}`");

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
