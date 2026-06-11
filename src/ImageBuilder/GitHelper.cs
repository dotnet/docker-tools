#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class GitHelper
    {
        public static string GetCommitSha(string filePath, bool useFullHash = false)
        {
            // Don't make the assumption that the current working directory is a Git repository
            // Find the Git repo that contains the file being checked.
            DirectoryInfo directory = new FileInfo(filePath).Directory;
            while (!directory.GetDirectories(".git").Any())
            {
                directory = directory.Parent;

                if (directory is null)
                {
                    throw new InvalidOperationException($"File '{filePath}' is not contained within a Git repository.");
                }
            }

            filePath = Path.GetRelativePath(directory.FullName, filePath);

            string format = useFullHash ? "H" : "h";
            return ExecuteHelper.Execute(
                new ProcessStartInfo("git", $"log -1 --format=format:%{format} {filePath}")
                {
                    WorkingDirectory = directory.FullName
                },
                false,
                $"Unable to retrieve the latest commit SHA for {filePath}");
        }

        public static Uri GetArchiveUrl(IGitHubBranchRef branchRef) =>
            new Uri($"https://github.com/{branchRef.Owner}/{branchRef.Repo}/archive/{branchRef.Branch}.zip");

        public static Uri GetBlobUrl(IGitHubFileRef fileRef) =>
            new Uri($"https://github.com/{fileRef.Owner}/{fileRef.Repo}/blob/{fileRef.Branch}/{fileRef.Path}");

        public static Uri GetCommitUrl(IGitHubRepoRef repoRef, string sha) =>
            new Uri($"https://github.com/{repoRef.Owner}/{repoRef.Repo}/commit/{sha}");
    }
}
