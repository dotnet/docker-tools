// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

/// <summary>Implements repository and pull request automation for Azure DevOps.</summary>
/// <remarks>Creates an endpoint for an Azure DevOps repository.</remarks>
/// <param name="client">The Azure DevOps Git HTTP client.</param>
/// <param name="repositoryIdOrName">The repository ID or name.</param>
/// <param name="accessToken">The token used to access the Git remote.</param>
/// <param name="identity">The identity used for automation commits.</param>
public sealed class AzureDevOpsPullRequestEndpoint(
    AzureDevOpsClient client,
    string repositoryIdOrName,
    string accessToken,
    AutomationIdentity identity)
        : IPullRequestEndpoint
{
    private const string HeadRefPrefix = "refs/heads/";

    private readonly AzureDevOpsClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly string _repositoryIdOrName = repositoryIdOrName
        ?? throw new ArgumentNullException(nameof(repositoryIdOrName));
    private readonly string _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
    private readonly AutomationIdentity _identity = identity ?? throw new ArgumentNullException(nameof(identity));

    private readonly Lazy<Task<AzureDevOpsRepository>> _repositoryDetails =
        new(() => client.GetRepositoryAsync(repositoryIdOrName, CancellationToken.None));

    /// <inheritdoc/>
    public async ValueTask<AutomationIdentity> GetIdentityAsync(CancellationToken cancellationToken)
    {
        return await ValueTask.FromResult(_identity);
    }

    /// <inheritdoc/>
    public async Task CloneSourceAsync(
        GitWorkingCopy workingCopy,
        string branch,
        AutomationIdentity identity,
        CancellationToken cancellationToken)
    {
        await CloneAsync(workingCopy, branch, identity, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CloneTargetAsync(
        GitWorkingCopy workingCopy,
        string branch,
        AutomationIdentity identity,
        CancellationToken cancellationToken)
    {
        await CloneAsync(workingCopy, branch, identity, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PushAsync(
        GitWorkingCopy workingCopy,
        string branch,
        bool force,
        CancellationToken cancellationToken)
    {
        AzureDevOpsRepository repository = await _repositoryDetails.Value.WaitAsync(cancellationToken);

        await workingCopy.PushAsync(
            new Uri(repository.RemoteUrl),
            GetAuthorizationHeaderAsync,
            branch,
            force,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ExistingPullRequest?> FindPullRequestAsync(string key, CancellationToken cancellationToken)
    {
        string sourceRefName = GetRefName(key);
        AzureDevOpsPullRequest[] pullRequests =
            await _client.ListActivePullRequestsAsync(_repositoryIdOrName, sourceRefName, cancellationToken);

        if (pullRequests.Length == 0)
        {
            return null;
        }

        if (pullRequests.Length > 1)
        {
            throw new InvalidOperationException(
                $"Expected at most one active pull request with source ref '{sourceRefName}' " +
                $"in repository '{_repositoryIdOrName}', but found {pullRequests.Length}.");
        }

        AzureDevOpsPullRequest pullRequest = pullRequests[0];
        AzureDevOpsCommit sourceCommit = await _client.GetCommitAsync(
            _repositoryIdOrName,
            pullRequest.LastMergeSourceCommit.CommitId,
            cancellationToken);

        string treeId = sourceCommit.TreeId
            ?? throw new InvalidOperationException(
                $"Azure DevOps did not return a tree ID for commit '{sourceCommit.CommitId}'.");

        List<CommitInfo> commits = [];
        IAsyncEnumerable<AzureDevOpsCommit> pullRequestCommits =
            _client.GetPullRequestCommits(_repositoryIdOrName, pullRequest.PullRequestId, cancellationToken);

        await foreach (AzureDevOpsCommit commit in pullRequestCommits)
        {
            commits.Add(CommitInfo.FromAzureDevOpsCommit(commit));
        }

        PullRequestState content = new PullRequestState(
            Key: key,
            Title: pullRequest.Title,
            Body: pullRequest.Description,
            TargetBranch: GetBranchName(pullRequest.TargetRefName),
            TreeHash: treeId);

        return new ExistingPullRequest(content, pullRequest.PullRequestId, new Uri(pullRequest.WebUrl), commits);
    }

    /// <inheritdoc/>
    public async Task<Uri> CreatePullRequestAsync(NewPullRequest pullRequest, CancellationToken cancellationToken)
    {
        AzureDevOpsCreatePullRequest request = new AzureDevOpsCreatePullRequest(
            SourceRefName: GetRefName(pullRequest.SourceBranch),
            TargetRefName: GetRefName(pullRequest.TargetBranch),
            Title: pullRequest.Title,
            Description: pullRequest.Body);

        AzureDevOpsPullRequest created =
            await _client.CreatePullRequestAsync(_repositoryIdOrName, request, cancellationToken);

        return new Uri(created.WebUrl);
    }

    /// <inheritdoc/>
    public async Task UpdatePullRequestAsync(
        int number,
        PullRequestChanges changes,
        CancellationToken cancellationToken)
    {
        AzureDevOpsUpdatePullRequest update = new AzureDevOpsUpdatePullRequest(
            changes.Title,
            changes.Body,
            changes.TargetBranch is null ? null : GetRefName(changes.TargetBranch));

        await _client.UpdatePullRequestAsync(_repositoryIdOrName, number, update, cancellationToken);
    }

    private async Task CloneAsync(
        GitWorkingCopy workingCopy,
        string branch,
        AutomationIdentity identity,
        CancellationToken cancellationToken)
    {
        AzureDevOpsRepository repository = await _repositoryDetails.Value.WaitAsync(cancellationToken);

        await workingCopy.CloneAsync(
            new Uri(repository.RemoteUrl),
            GetAuthorizationHeaderAsync,
            branch,
            identity,
            cancellationToken);
    }

    private async ValueTask<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken)
    {
        return await ValueTask.FromResult($"Bearer {_accessToken}");
    }

    // GitAutomation uses short branch names, while the Azure DevOps REST API uses full refs.
    private static string GetRefName(string branch)
    {
        return $"{HeadRefPrefix}{branch}";
    }

    private static string GetBranchName(string refName)
    {
        return refName.StartsWith(HeadRefPrefix, StringComparison.Ordinal)
            ? refName[HeadRefPrefix.Length..]
            : refName;
    }
}
