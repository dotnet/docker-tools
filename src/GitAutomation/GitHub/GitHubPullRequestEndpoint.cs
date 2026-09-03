// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Octokit;

namespace Microsoft.DotNet.GitAutomation.GitHub;

/// <summary>Provides GitHub repository and pull request operations.</summary>
/// <remarks>Creates an endpoint for an upstream repository and optional fork.</remarks>
/// <param name="accessProvider">Provides repository-specific GitHub access.</param>
/// <param name="upstream">The repository that receives pull requests.</param>
/// <param name="fork">The repository that receives source branches, or <see langword="null"/> to use the upstream.</param>
public sealed class GitHubPullRequestEndpoint(
    IGitHubAccessProvider accessProvider,
    GitHubRepo upstream,
    GitHubRepo? fork = null)
        : IPullRequestEndpoint
{
    private readonly IGitHubAccessProvider _accessProvider = accessProvider
        ?? throw new ArgumentNullException(nameof(accessProvider));

    private readonly GitHubRepo _upstream = upstream
        ?? throw new ArgumentNullException(nameof(upstream));

    private readonly GitHubRepo _source = fork ?? upstream;

    /// <inheritdoc/>
    public async ValueTask<AutomationIdentity> GetIdentityAsync(CancellationToken cancellationToken)
    {
        GitHubAccess sourceAccess = await GetRepositoryAccessAsync(_source, cancellationToken);
        return sourceAccess.Identity;
    }

    /// <inheritdoc/>
    public async Task CloneSourceAsync(
        GitWorkingCopy workingCopy,
        string branch,
        AutomationIdentity identity,
        CancellationToken cancellationToken)
    {
        GitHubAccess access = await GetRepositoryAccessAsync(_source, cancellationToken);
        await CloneAsync(workingCopy, _source, access, branch, identity, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CloneTargetAsync(
        GitWorkingCopy workingCopy,
        string branch,
        AutomationIdentity identity,
        CancellationToken cancellationToken)
    {
        GitHubAccess access = await GetRepositoryAccessAsync(_upstream, cancellationToken);
        await CloneAsync(workingCopy, _upstream, access, branch, identity, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PushAsync(
        GitWorkingCopy workingCopy,
        string branch,
        bool force,
        CancellationToken cancellationToken)
    {
        GitHubAccess access = await GetRepositoryAccessAsync(_source, cancellationToken);

        await workingCopy.PushAsync(
            _source.GetCloneUrl(),
            ct => GetAuthorizationHeaderAsync(access, ct),
            branch,
            force,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ExistingPullRequest?> FindPullRequestAsync(string key, CancellationToken cancellationToken)
    {
        // Throughout method: check cancellationToken after each Octokit call
        // because Octokit does not accept cancellation tokens.

        GitHubAccess targetAccess = await GetRepositoryAccessAsync(_upstream, cancellationToken);
        IPullRequestsClient pullRequestsClient = targetAccess.Client.PullRequest;

        PullRequestRequest query = new PullRequestRequest
        {
            Head = _source.GetHeadRef(key),
            State = ItemStateFilter.Open,
        };

        var pullRequests = await pullRequestsClient.GetAllForRepository(_upstream.Owner, _upstream.Name, query);
        cancellationToken.ThrowIfCancellationRequested();

        if (pullRequests.Count == 0)
        {
            return null;
        }

        if (pullRequests.Count > 1)
        {
            throw new InvalidOperationException(
                $"Expected at most one open pull request with head '{_source.GetHeadRef(key)}'"
                + $" in {_upstream.Owner}/{_upstream.Name}, but found {pullRequests.Count}.");
        }

        PullRequest pullRequest = pullRequests[0];
        GitHubAccess sourceAccess = await GetRepositoryAccessAsync(_source, cancellationToken);

        Commit headCommit = await sourceAccess.Client.Git.Commit.Get(_source.Owner, _source.Name, pullRequest.Head.Sha);
        cancellationToken.ThrowIfCancellationRequested();

        var pullRequestCommits = await pullRequestsClient.Commits(_upstream.Owner, _upstream.Name, pullRequest.Number);
        cancellationToken.ThrowIfCancellationRequested();

        PullRequestState content = new PullRequestState(
            Key: key,
            Title: pullRequest.Title,
            Body: pullRequest.Body ?? string.Empty,
            TargetBranch: pullRequest.Base.Ref,
            TreeHash: headCommit.Tree.Sha);

        Uri pullRequestUri = new Uri(pullRequest.HtmlUrl);
        CommitInfo[] commitInfos = pullRequestCommits.Select(CommitInfo.FromPullRequestCommit).ToArray();
        return new ExistingPullRequest(content, pullRequest.Number, pullRequestUri, commitInfos);
    }

    /// <inheritdoc/>
    public async Task<Uri> CreatePullRequestAsync(NewPullRequest pullRequest, CancellationToken cancellationToken)
    {
        GitHubAccess access = await GetRepositoryAccessAsync(_upstream, cancellationToken);
        string sourceBranch = _source.GetHeadRef(pullRequest.SourceBranch);

        Octokit.NewPullRequest request = new Octokit.NewPullRequest(
            title: pullRequest.Title,
            head: sourceBranch,
            baseRef: pullRequest.TargetBranch)
        {
            Body = pullRequest.Body,
        };

        PullRequest created = await access.Client.PullRequest.Create(_upstream.Owner, _upstream.Name, request);
        return new Uri(created.HtmlUrl);
    }

    /// <inheritdoc/>
    public async Task UpdatePullRequestAsync(
        int number,
        PullRequestChanges changes,
        CancellationToken cancellationToken)
    {
        GitHubAccess access = await GetRepositoryAccessAsync(_upstream, cancellationToken);

        PullRequestUpdate update = new PullRequestUpdate
        {
            Title = changes.Title,
            Body = changes.Body,
            Base = changes.TargetBranch,
        };

        await access.Client.PullRequest.Update(_upstream.Owner, _upstream.Name, number, update);
    }

    private async ValueTask<GitHubAccess> GetRepositoryAccessAsync(
        GitHubRepo repository,
        CancellationToken cancellationToken)
    {
        GitHubAccess access = await _accessProvider.GetAccessAsync(repository, cancellationToken);
        return access;
    }

    private static Task CloneAsync(
        GitWorkingCopy workingCopy,
        GitHubRepo repository,
        GitHubAccess access,
        string branch,
        AutomationIdentity identity,
        CancellationToken cancellationToken)
    {
        return workingCopy.CloneAsync(
            repository.GetCloneUrl(),
            ct => GetAuthorizationHeaderAsync(access, ct),
            branch,
            identity,
            cancellationToken);
    }

    private static async ValueTask<string> GetAuthorizationHeaderAsync(
        GitHubAccess access,
        CancellationToken cancellationToken)
    {
        Credentials credentials = await access.Credentials.GetCredentials();
        cancellationToken.ThrowIfCancellationRequested();

        string value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{credentials.Password}"));

        return $"Basic {value}";
    }
}
