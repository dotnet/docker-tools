// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation.GitHub;

public sealed record GitHubRepo(string Owner, string Name);

internal static class GitHubRepoExtensions
{
    public static Uri GetCloneUrl(this GitHubRepo repo) =>
        new($"https://github.com/{repo.Owner}/{repo.Name}");

    public static Uri GetAuthenticatedCloneUrl(this GitHubRepo repo, string token) =>
        new($"https://x-access-token:{token}@github.com/{repo.Owner}/{repo.Name}");

    public static Uri GetCommitUrl(this GitHubRepo repo, string sha) =>
        new($"https://github.com/{repo.Owner}/{repo.Name}/commit/{sha}");

    public static string GetHeadRef(this GitHubRepo repo, string branch) => $"{repo.Owner}:{branch}";
}
