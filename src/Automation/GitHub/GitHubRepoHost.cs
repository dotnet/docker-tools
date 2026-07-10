// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Octokit;

namespace Microsoft.DotNet.Automation.GitHub;

internal sealed class GitHubRepoHost(
    GitHubRepo targetRepo,
    GitHubRepo sourceRepo,
    string token,
    IGitHubClient client,
    ILoggerFactory loggerFactory,
    Git git
) : IRepoHost
{
    private readonly ILogger<GitHubRepoHost> _logger = loggerFactory.CreateLogger<GitHubRepoHost>();

    public async Task<ExistingPullRequest?> GetPullRequest(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = new PullRequestRequest
        {
            Head = sourceRepo.GetHeadRef(key),
            State = ItemStateFilter.Open,
        };

        IReadOnlyList<PullRequest> pullRequests =
            await client.PullRequest.GetAllForRepository(targetRepo.Owner, targetRepo.Name, query);

        if (pullRequests.Count == 0)
        {
            _logger.LogDebug(
                "No open pull request with head '{Head}' in {Owner}/{Name}.",
                sourceRepo.GetHeadRef(key),
                targetRepo.Owner,
                targetRepo.Name);

            return null;
        }

        if (pullRequests.Count > 1)
        {
            throw new InvalidOperationException(
                $"Expected at most one open pull request with head '{sourceRepo.GetHeadRef(key)}' " +
                $"in {targetRepo.Owner}/{targetRepo.Name}, but found {pullRequests.Count}.");
        }

        PullRequest pullRequest = pullRequests[0];
        cancellationToken.ThrowIfCancellationRequested();
        Commit headCommit = await client.Git.Commit.Get(sourceRepo.Owner, sourceRepo.Name, pullRequest.Head.Sha);

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<PullRequestCommit> pullRequestCommits =
            await client.PullRequest.Commits(targetRepo.Owner, targetRepo.Name, pullRequest.Number);

        IReadOnlyList<CommitInfo> commits = pullRequestCommits
            .Select(commit => new CommitInfo(commit.Sha, commit.Commit.Author.Name, commit.Commit.Author.Email))
            .ToArray();

        _logger.LogDebug(
            "Found open pull request #{Number} with head '{Head}' ({CommitCount} commit(s)).",
            pullRequest.Number,
            sourceRepo.GetHeadRef(key),
            commits.Count);

        var pullRequestState = new PullRequestState(
            key,
            pullRequest.Title,
            pullRequest.Body ?? string.Empty,
            pullRequest.Base.Ref,
            headCommit.Tree.Sha);

        return new ExistingPullRequest(
            pullRequestState,
            pullRequest.Number,
            new Uri(pullRequest.HtmlUrl),
            commits
        );
    }

    public async Task<IReadOnlyList<IOperationResult>> ExecuteAsync(IEnumerable<IOperation> operations, CancellationToken cancellationToken)
    {
        List<IOperationResult> results = [];

        foreach (IOperation operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IOperationResult result = operation switch
            {
                PushCommitsOperation push => await PushAsync(push, cancellationToken),
                CreatePullRequestOperation create => await CreatePullRequestAsync(create, cancellationToken),
                UpdateTitleOperation updateTitle => await UpdateTitleAsync(updateTitle, cancellationToken),
                UpdateBodyOperation updateBody => await UpdateBodyAsync(updateBody, cancellationToken),
                UpdateBaseBranchOperation updateBase => await UpdateBaseBranchAsync(updateBase, cancellationToken),
                _ => throw new InvalidOperationException($"Unknown operation type '{operation.GetType()}'."),
            };

            results.Add(result);
        }

        return results;
    }

    private async Task<CommitsPushed> PushAsync(PushCommitsOperation operation, CancellationToken cancellationToken)
    {
        string authUrl = sourceRepo.GetAuthenticatedCloneUrl(token).AbsoluteUri;
        string branch = operation.SourceBranch;
        string dir = operation.WorkspaceDirectory;
        string remoteRef = $"refs/heads/{branch}";

        string lsRemote = await git.RunAsync(token, dir, cancellationToken, ["ls-remote", "--heads", authUrl, remoteRef]);
        string? existingLine = lsRemote
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.EndsWith($"\t{remoteRef}", StringComparison.Ordinal));

        string fromSha = existingLine is null ? string.Empty : existingLine.Split('\t')[0];
        string toSha = await git.RunAsync(secret: null, dir, cancellationToken, "rev-parse", "HEAD");

        bool forcePush = operation.ForcePush;
        string[] pushArgs = forcePush
            ? ["push", "--force", authUrl, $"HEAD:{remoteRef}"]
            : ["push", authUrl, $"HEAD:{remoteRef}"];

        _logger.LogInformation(
            "Pushing commit {ToSha} to branch '{Branch}' in {Owner}/{Name}{Force}.",
            toSha, branch, sourceRepo.Owner, sourceRepo.Name, forcePush ? " (force)" : string.Empty);

        await git.RunAsync(token, dir, cancellationToken, pushArgs);

        Uri commitUrl = sourceRepo.GetCommitUrl(toSha);
        _logger.LogInformation(
            "Pushed branch '{Branch}' from {FromSha} to {ToSha}: {Url}",
            branch, fromSha.Length == 0 ? "(new branch)" : fromSha, toSha, commitUrl);

        return new CommitsPushed(branch, fromSha, toSha, commitUrl);
    }

    private async Task<PullRequestCreated> CreatePullRequestAsync(CreatePullRequestOperation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var head = sourceRepo.GetHeadRef(operation.SourceBranch);
        var newPullRequest = new NewPullRequest(operation.Title, head, operation.TargetBranch)
        {
            Body = operation.Body,
        };

        _logger.LogInformation(
            "Creating pull request '{Title}' from '{Head}' into '{Base}' in {Owner}/{Name}.",
            operation.Title,
            head,
            operation.TargetBranch,
            targetRepo.Owner,
            targetRepo.Name);

        PullRequest created = await client.PullRequest.Create(targetRepo.Owner, targetRepo.Name, newPullRequest);

        _logger.LogInformation("Created pull request #{Number}: {Url}.", created.Number, created.HtmlUrl);
        return new PullRequestCreated(created.Number, new Uri(created.HtmlUrl));
    }

    private async Task<TitleUpdated> UpdateTitleAsync(UpdateTitleOperation operation, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating title of pull request #{Number} to '{Title}'.",
            operation.Number,
            operation.Title);

        await UpdatePullRequestAsync(operation.Number, cancellationToken, title: operation.Title);

        _logger.LogInformation("Updated title of pull request #{Number}.", operation.Number);
        return new TitleUpdated(operation.Number, operation.Title);
    }

    private async Task<BodyUpdated> UpdateBodyAsync(UpdateBodyOperation operation, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating body of pull request #{Number}.", operation.Number);
        await UpdatePullRequestAsync(operation.Number, cancellationToken, body: operation.Body);
        _logger.LogInformation("Updated body of pull request #{Number}.", operation.Number);
        return new BodyUpdated(operation.Number, operation.Body);
    }

    private async Task<BaseBranchUpdated> UpdateBaseBranchAsync(UpdateBaseBranchOperation operation, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating base branch of pull request #{Number} to '{TargetBranch}'.",
            operation.Number,
            operation.TargetBranch);

        await UpdatePullRequestAsync(operation.Number, cancellationToken, baseBranch: operation.TargetBranch);

        _logger.LogInformation("Updated base branch of pull request #{Number}.", operation.Number);
        return new BaseBranchUpdated(operation.Number, operation.TargetBranch);
    }

    private async Task UpdatePullRequestAsync(int number, CancellationToken cancellationToken, string? title = null, string? body = null, string? baseBranch = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var update = new PullRequestUpdate { Title = title, Body = body, Base = baseBranch };
        await client.PullRequest.Update(targetRepo.Owner, targetRepo.Name, number, update);
    }
}
