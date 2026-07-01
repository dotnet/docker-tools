// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Octokit;

namespace Microsoft.DotNet.Automation.GitHub;

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
        _engine = new RepoHostEngine(
            repo,
            headRepo,
            new GitHubPullRequestApi(gitHubClient, repo, headRepo),
            options,
            loggerFactory);
    }

    /// <inheritdoc/>
    public Task<PullRequestResult> EnsurePullRequestAsync(PullRequestSpec spec, CancellationToken cancellationToken = default) =>
        _engine.EnsurePullRequestAsync(spec, cancellationToken);

    /// <inheritdoc/>
    public Task<BranchResult> EnsureBranchContentAsync(BranchSpec spec, CancellationToken cancellationToken = default) =>
        _engine.EnsureBranchContentAsync(spec, cancellationToken);

    private static IGitHubClient CreateGitHubClient(string token)
    {
        GitHubClient client = new(new ProductHeaderValue("Microsoft.DotNet.Automation"));
        if (!string.IsNullOrEmpty(token))
        {
            client.Credentials = new Credentials(token);
        }

        return client;
    }

}
