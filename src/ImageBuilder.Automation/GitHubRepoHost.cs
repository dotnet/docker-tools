// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Octokit;

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// An <see cref="IRepoHost"/> for repositories hosted on GitHub. Branches are
/// pushed with the git CLI; the GitHub API is only used to manage pull
/// requests and comments.
/// </summary>
public sealed class GitHubRepoHost : IRepoHost
{
    private readonly RepoHostEngine _engine;

    /// <param name="repo">The repository that pull requests merge into and branches are pushed to.</param>
    /// <param name="options">Common automation settings.</param>
    /// <param name="headRepo">
    /// The repository that pull request head branches are pushed to. Specify a
    /// fork here to avoid pushing branches to <paramref name="repo"/> itself.
    /// </param>
    public GitHubRepoHost(
        GitHubRepo repo,
        GitAutomationOptions options,
        GitHubRepo? headRepo = null,
        ILoggerFactory? loggerFactory = null)
        : this(repo, options, headRepo, loggerFactory, CreateGitHubClient(options.Token))
    {
    }

    internal GitHubRepoHost(
        GitHubRepo repo,
        GitAutomationOptions options,
        GitHubRepo? headRepo,
        ILoggerFactory? loggerFactory,
        IGitHubClient gitHubClient)
    {
        headRepo ??= repo;
        ILogger logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<GitHubRepoHost>();
        _engine = new RepoHostEngine(
            repo,
            headRepo,
            new GitHubPullRequestApi(gitHubClient, repo, headRepo),
            options,
            logger);
    }

    /// <inheritdoc/>
    public Task<EnsureResult> EnsurePullRequestAsync(PullRequestSpec spec, CancellationToken cancellationToken = default) =>
        _engine.EnsurePullRequestAsync(spec, cancellationToken);

    /// <inheritdoc/>
    public Task<EnsureResult> EnsureBranchAsync(BranchSpec spec, CancellationToken cancellationToken = default) =>
        _engine.EnsureBranchAsync(spec, cancellationToken);

    private static IGitHubClient CreateGitHubClient(string token)
    {
        GitHubClient client = new(new ProductHeaderValue("Microsoft.DotNet.ImageBuilder.Automation"));
        if (!string.IsNullOrEmpty(token))
        {
            client.Credentials = new Credentials(token);
        }

        return client;
    }

    /// <summary>
    /// Pull request operations backed by the GitHub REST API. The Octokit
    /// client does not accept cancellation tokens, so they are ignored here.
    /// </summary>
    private sealed class GitHubPullRequestApi(
        IGitHubClient client,
        GitHubRepo targetRepo,
        GitHubRepo headRepo) : IPullRequestApi
    {
        public async Task<PullRequestInfo?> FindOpenAsync(
            string headBranch, string targetBranch, CancellationToken cancellationToken)
        {
            IReadOnlyList<PullRequest> pullRequests = await client.PullRequest.GetAllForRepository(
                targetRepo.Owner,
                targetRepo.Name,
                new PullRequestRequest
                {
                    Head = GetHeadRef(headBranch),
                    Base = targetBranch,
                    State = ItemStateFilter.Open,
                });

            return pullRequests.Count == 0 ? null : ToInfo(pullRequests[0]);
        }

        public async Task<PullRequestInfo> CreateAsync(
            string title, string body, string headBranch, string targetBranch, CancellationToken cancellationToken)
        {
            PullRequest pullRequest = await client.PullRequest.Create(
                targetRepo.Owner,
                targetRepo.Name,
                new NewPullRequest(title, GetHeadRef(headBranch), targetBranch)
                {
                    Body = body,
                });

            return ToInfo(pullRequest);
        }

        public Task UpdateAsync(long id, string title, string body, CancellationToken cancellationToken) =>
            client.PullRequest.Update(
                targetRepo.Owner,
                targetRepo.Name,
                (int)id,
                new PullRequestUpdate
                {
                    Title = title,
                    Body = body,
                });

        public async Task<IReadOnlyList<string>> GetCommentsAsync(long id, CancellationToken cancellationToken)
        {
            IReadOnlyList<IssueComment> comments =
                await client.Issue.Comment.GetAllForIssue(targetRepo.Owner, targetRepo.Name, (int)id);
            return [.. comments.Select(comment => comment.Body)];
        }

        public Task AddCommentAsync(long id, string comment, CancellationToken cancellationToken) =>
            client.Issue.Comment.Create(targetRepo.Owner, targetRepo.Name, (int)id, comment);

        // GitHub identifies pull request head branches as "owner:branch".
        private string GetHeadRef(string headBranch) => $"{headRepo.Owner}:{headBranch}";

        private static PullRequestInfo ToInfo(PullRequest pullRequest) =>
            new(pullRequest.Number, pullRequest.HtmlUrl, pullRequest.Title, pullRequest.Body ?? string.Empty);
    }
}
