// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

/// <summary>Describes an Azure DevOps Git repository.</summary>
/// <param name="Id">The repository ID.</param>
/// <param name="Name">The repository name.</param>
/// <param name="RemoteUrl">The Git remote URL.</param>
/// <param name="WebUrl">The browser URL.</param>
public sealed record AzureDevOpsRepository(string Id, string Name, string RemoteUrl, string WebUrl);

/// <summary>Describes an Azure DevOps pull request.</summary>
/// <param name="PullRequestId">The pull request ID.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description.</param>
/// <param name="SourceRefName">The full source ref name.</param>
/// <param name="TargetRefName">The full target ref name.</param>
/// <param name="Repository">The target repository.</param>
/// <param name="LastMergeSourceCommit">The latest source commit.</param>
public sealed record AzureDevOpsPullRequest(
    int PullRequestId,
    string Title,
    string Description,
    string SourceRefName,
    string TargetRefName,
    AzureDevOpsRepository Repository,
    AzureDevOpsCommitReference LastMergeSourceCommit)
{
    /// <summary>Gets the browser URL for the pull request.</summary>
    public string WebUrl => $"{Repository.RemoteUrl.TrimEnd('/')}/pullrequest/{PullRequestId}";
}

/// <summary>Identifies an Azure DevOps Git commit.</summary>
/// <param name="CommitId">The commit ID.</param>
public sealed record AzureDevOpsCommitReference(string CommitId);

/// <summary>Describes an Azure DevOps Git commit.</summary>
/// <param name="CommitId">The commit ID.</param>
/// <param name="TreeId">The tree ID, when returned.</param>
/// <param name="Author">The commit author.</param>
public sealed record AzureDevOpsCommit(string CommitId, string? TreeId, AzureDevOpsGitUser Author);

/// <summary>Describes an Azure DevOps Git user.</summary>
/// <param name="Name">The user name.</param>
/// <param name="Email">The email address.</param>
public sealed record AzureDevOpsGitUser(string Name, string Email);

/// <summary>Defines a new Azure DevOps pull request.</summary>
/// <param name="SourceRefName">The full source ref name.</param>
/// <param name="TargetRefName">The full target ref name.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description.</param>
public sealed record AzureDevOpsCreatePullRequest(
    string SourceRefName,
    string TargetRefName,
    string Title,
    string Description);

/// <summary>Defines changes to an Azure DevOps pull request.</summary>
/// <param name="Title">The new title.</param>
/// <param name="Description">The new description.</param>
/// <param name="TargetRefName">The new full target ref name.</param>
public sealed record AzureDevOpsUpdatePullRequest(
    string? Title = null,
    string? Description = null,
    string? TargetRefName = null);

internal sealed record ArrayResponse<T>(int Count, T[] Value);
