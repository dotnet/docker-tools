// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Octokit;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class TreesClientExtensions
    {
        public static async Task<string> GetFileShaAsync(
            this ITreesClient treesClient, string repoOwner, string repoName, string branch, string path)
        {
            string? dirPath = Path.GetDirectoryName(path)?.Replace("\\", "/");
            TreeResponse treeResponse = await treesClient.Get(repoOwner, repoName, HttpUtility.UrlEncode($"{branch}:{dirPath}"));
            string fileName = Path.GetFileName(path);
            TreeItem? item = treeResponse.Tree
                .FirstOrDefault(item => string.Equals(item.Path, fileName, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                throw new InvalidOperationException(
                    $"Unable to find git tree data for path '{path}' in repo '{repoName}'.");
            }

            return item.Sha;
        }
    }
}
