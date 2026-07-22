#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class GitHelper
    {
        private const int DefaultMaxTries = 10;
        private const int DefaultRetryMillisecondsDelay = 5000;

        public static string GetCommitSha(string filePath, bool useFullHash = false)
        {
            string repoRoot = GetRepoRoot(filePath);
            filePath = Path.GetRelativePath(repoRoot, filePath);

            string format = useFullHash ? "H" : "h";
            return ExecuteHelper.Execute(
                new ProcessStartInfo("git", $"log -1 --format=format:%{format} {filePath}")
                {
                    WorkingDirectory = repoRoot
                },
                false,
                $"Unable to retrieve the latest commit SHA for {filePath}");
        }

        // Don't make the assumption that the current working directory is a Git repository.
        // Walk up from the given path to find the root of the containing Git repository.
        public static string GetRepoRoot(string path)
        {
            DirectoryInfo directory = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path).Directory;

            // The repository root is marked by a ".git" entry. It's a directory in a normal
            // checkout, but a file (a gitdir pointer) in linked worktrees and submodules.
            while (!directory.EnumerateFileSystemInfos(".git").Any())
            {
                directory = directory.Parent
                    ?? throw new InvalidOperationException($"'{path}' is not contained within a Git repository.");
            }

            return directory.FullName;
        }

        public static Uri GetArchiveUrl(IGitHubBranchRef branchRef) =>
            new Uri($"https://github.com/{branchRef.Owner}/{branchRef.Repo}/archive/{branchRef.Branch}.zip");

        public static Uri GetBlobUrl(IGitHubFileRef fileRef) =>
            new Uri($"https://github.com/{fileRef.Owner}/{fileRef.Repo}/blob/{fileRef.Branch}/{fileRef.Path}");

        public static Uri GetCommitUrl(IGitHubRepoRef repoRef, string sha) =>
            new Uri($"https://github.com/{repoRef.Owner}/{repoRef.Repo}/commit/{sha}");

        public static async Task<GitReference> PushChangesAsync(IGitHubClient client, IGitOptionsHost options, string commitMessage, Func<GitHubBranch, Task<IEnumerable<GitObject>>> getChanges)
        {
            GitOptions gitOptions = options.GitOptions;
            GitHubProject project = new GitHubProject(gitOptions.Repo, gitOptions.Owner);
            GitHubBranch branch = new GitHubBranch(gitOptions.Branch, project);

            IEnumerable<GitObject> changes = await getChanges(branch);

            if (!changes.Any())
            {
                return null;
            }

            string masterRef = $"heads/{gitOptions.Branch}";
            GitReference currentMaster = await client.GetReferenceAsync(project, masterRef);
            string masterSha = currentMaster.Object.Sha;

            if (!options.IsDryRun)
            {
                GitTree tree = await client.PostTreeAsync(project, masterSha, changes.ToArray());
                GitCommit commit = await client.PostCommitAsync(
                    project, commitMessage, tree.Sha, new[] { masterSha });

                // Only fast-forward. Don't overwrite other changes: throw exception instead.
                return await client.PatchReferenceAsync(project, masterRef, commit.Sha, force: false);
            }
            else
            {
                ILogger logger = StandaloneLoggerFactory.CreateLogger(nameof(GitHelper));
                logger.LogInformation($"The following files would have been updated at {gitOptions.Owner}/{gitOptions.Repo}/{gitOptions.Branch}:");
                logger.LogInformation(string.Empty);
                foreach (GitObject gitObject in changes)
                {
                    logger.LogInformation($"{gitObject.Path}:");
                    logger.LogInformation(gitObject.Content);
                    logger.LogInformation(string.Empty);
                }

                return null;
            }
        }
    }
}
