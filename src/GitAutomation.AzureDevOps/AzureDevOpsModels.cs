// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

/// <summary>Describes an Azure DevOps Git repository.</summary>
/// <param name="Id">The repository ID.</param>
/// <param name="Name">The repository name.</param>
/// <param name="RemoteUrl">The Git remote URL.</param>
/// <param name="WebUrl">The browser URL.</param>
public sealed record Repository(string Id, string Name, string RemoteUrl, string WebUrl);

/// <summary>Identifies a pull request returned from a search.</summary>
/// <param name="PullRequestId">The pull request ID.</param>
public sealed record PullRequestSearchResult(int PullRequestId);

/// <summary>Describes an Azure DevOps pull request.</summary>
/// <param name="PullRequestId">The pull request ID.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description.</param>
/// <param name="SourceRefName">The full source ref name.</param>
/// <param name="TargetRefName">The full target ref name.</param>
/// <param name="Repository">The target repository.</param>
/// <param name="LastMergeSourceCommit">The latest source commit.</param>
public sealed record PullRequest(
    int PullRequestId,
    string Title,
    string Description,
    string SourceRefName,
    string TargetRefName,
    Repository Repository,
    CommitReference LastMergeSourceCommit)
{
    /// <summary>Gets the browser URL for the pull request.</summary>
    public string WebUrl => $"{Repository.RemoteUrl.TrimEnd('/')}/pullrequest/{PullRequestId}";
}

/// <summary>Identifies an Azure DevOps Git commit.</summary>
/// <param name="CommitId">The commit ID.</param>
public sealed record CommitReference(string CommitId);

/// <summary>Describes an Azure DevOps Git commit.</summary>
/// <param name="CommitId">The commit ID.</param>
/// <param name="TreeId">The tree ID, when returned.</param>
/// <param name="Author">The commit author.</param>
public sealed record Commit(string CommitId, string? TreeId, GitUser Author);

/// <summary>Describes an Azure DevOps Git user.</summary>
/// <param name="Name">The user name.</param>
/// <param name="Email">
/// The Git author email, or <see langword="null"/> when the commit does not include one.
/// Azure DevOps also redacts this value from unauthenticated API responses.
/// </param>
public sealed record GitUser(string Name, string? Email);

/// <summary>Defines a new Azure DevOps pull request.</summary>
/// <param name="SourceRefName">The full source ref name.</param>
/// <param name="TargetRefName">The full target ref name.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description.</param>
public sealed record CreatePullRequest(
    string SourceRefName,
    string TargetRefName,
    string Title,
    string Description);

/// <summary>Defines changes to an Azure DevOps pull request.</summary>
/// <param name="Title">The new title.</param>
/// <param name="Description">The new description.</param>
/// <param name="TargetRefName">The new full target ref name.</param>
public sealed record UpdatePullRequest(
    string? Title = null,
    string? Description = null,
    string? TargetRefName = null);

internal sealed record ArrayResponse<T>(int Count, T[] Value);
