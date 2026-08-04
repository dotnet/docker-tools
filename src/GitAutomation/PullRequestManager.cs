// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.GitAutomation.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.GitAutomation;

/// <summary>
/// Creates or updates a pull request to match a definition: clone the branch the
/// pull request is built from, apply the caller's changes, commit, then plan and
/// execute the operations needed to reconcile the pull request.
/// </summary>
public sealed class PullRequestManager
{
    private readonly IRepoHostProvider _repoHostProvider;
    private readonly Git _git;
    private readonly ILogger _gitLogger;
    private readonly ILogger<PullRequestManager> _logger;

    /// <summary>
    /// Creates a manager with caller-provided services.
    /// </summary>
    /// <param name="accessProvider">Provides credentials and identity for GitHub operations.</param>
    /// <param name="processRunner">Runs the git processes used during reconciliation.</param>
    /// <param name="loggerFactory">Creates the loggers used to trace the reconciliation.</param>
    public PullRequestManager(
        IGitHubAccessProvider accessProvider,
        IProcessRunner processRunner,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(accessProvider);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _logger = loggerFactory.CreateLogger<PullRequestManager>();
        _gitLogger = loggerFactory.CreateLogger(nameof(Git));
        _git = new Git(processRunner, _gitLogger);
        _repoHostProvider = new GitHubRepoHostProvider(accessProvider, loggerFactory, _git);
    }

    /// <summary>
    /// Creates a manager with a caller-provided repository access provider and the default
    /// <see cref="ProcessRunner"/>.
    /// </summary>
    /// <param name="accessProvider">Provides credentials and identity for GitHub operations.</param>
    /// <param name="loggerFactory">
    /// Creates the loggers used to trace the reconciliation. Omit (or pass
    /// <see langword="null"/>) to disable logging.
    /// </param>
    public PullRequestManager(
        IGitHubAccessProvider accessProvider,
        ILoggerFactory? loggerFactory = null)
        : this(
            accessProvider,
            CreateDefaultProcessRunner(loggerFactory),
            loggerFactory ?? NullLoggerFactory.Instance)
    {
    }

    /// <summary>
    /// Creates a manager with a fixed token and the default <see cref="ProcessRunner"/>.
    /// </summary>
    /// <param name="token">An access token with permission to push and open pull requests.</param>
    /// <param name="identity">The git identity used for the automation's commits.</param>
    /// <param name="loggerFactory">
    /// Creates the loggers used to trace the reconciliation. Omit (or pass
    /// <see langword="null"/>) to disable logging.
    /// </param>
    public PullRequestManager(
        string token,
        AutomationIdentity identity,
        ILoggerFactory? loggerFactory = null)
        : this(
            new StaticGitHubAccessProvider(token, identity),
            loggerFactory)
    {
    }

    /// <summary>
    /// Creates the pull request if it does not exist, or updates it to match
    /// the definition if it has drifted.
    /// </summary>
    /// <param name="definition">The desired pull request state and changes.</param>
    /// <param name="upstream">The repository the pull request is opened against.</param>
    /// <param name="fork">
    /// The repository commits are pushed to. Omit (or pass <see langword="null"/>) to
    /// push directly to <paramref name="upstream"/> without a fork.
    /// </param>
    /// <param name="updateStrategy">How an existing pull request branch is updated.</param>
    /// <param name="onForeignCommits">How commits from other authors are handled.</param>
    /// <param name="cancellationToken">A token that cancels the reconciliation.</param>
    public async Task<PullRequestResult> CreateOrUpdateAsync(
        PullRequestDefinition definition,
        GitHubRepo upstream,
        GitHubRepo? fork = null,
        PullRequestUpdateStrategy updateStrategy = PullRequestUpdateStrategy.Append,
        ForeignCommitPolicy onForeignCommits = ForeignCommitPolicy.Proceed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(upstream);

        IRepoHost host = await _repoHostProvider.OpenAsync(upstream, fork ?? upstream, cancellationToken);
        AutomationIdentity identity = host.Identity;

        _logger.LogInformation(
            "Creating or updating pull request for branch '{Key}' into '{TargetBranch}'.",
            definition.Key,
            definition.TargetBranch);

        // Fetch the existing pull request first: it decides which branch we build on.
        ExistingPullRequest? existing = await host.GetPullRequest(definition.Key, cancellationToken);

        if (existing is null)
        {
            _logger.LogInformation("No open pull request found for branch '{Key}'.", definition.Key);
        }
        else
        {
            _logger.LogInformation(
                "Found open pull request #{Number} ({Url}) with {CommitCount} commit(s) on its branch.",
                existing.Number,
                existing.Url,
                existing.Commits.Count);
        }

        // Always build on the branch the pull request is *from* (the source branch), never
        // the target branch we merge into. When an Append update targets an existing pull
        // request, its source branch already has our previous commits, so cloning it lets new
        // commits stack on top and the push fast-forward. Otherwise branch fresh from the
        // target branch: there is nothing to stack on (no pull request), or Replace will
        // overwrite the branch entirely with a force-push.
        bool appendToExistingPullRequest = existing is not null && updateStrategy == PullRequestUpdateStrategy.Append;
        GitHubRepo pullRequestSourceRepo = fork ?? upstream;
        GitHubRepo cloneRepo = appendToExistingPullRequest ? pullRequestSourceRepo : upstream;
        string cloneBranch = appendToExistingPullRequest ? definition.Key : definition.TargetBranch;
        string token = await host.GetTokenAsync();

        _logger.LogInformation("Cloning {Url} branch '{Branch}'.", cloneRepo.GetCloneUrl(), cloneBranch);

        using GitWorkspace workspace = await GitWorkspace.CloneAsync(
            _git,
            _gitLogger,
            cloneRepo.GetAuthenticatedCloneUrl(token),
            cloneBranch,
            identity.AuthorName,
            identity.AuthorEmail,
            cancellationToken);

        GitContext gitContext = new(workspace.WorkingDirectory, _git, _gitLogger);

        string clonedCommit = await _git.RunAsync(
            secret: null, workspace.WorkingDirectory, cancellationToken, "rev-parse", "HEAD");

        _logger.LogInformation(
            "Cloned branch '{Branch}' at commit {Commit} into {Directory}.",
            cloneBranch,
            clonedCommit,
            workspace.WorkingDirectory);

        // Capture the target branch's tree before applying changes so the Planner can
        // tell whether the caller's changes actually produced a diff worth proposing.
        // This only informs the no-pull-request case, where the base *is* the target branch.
        string targetBranchTreeHash = await _git.RunAsync(
            secret: null,
            workspace.WorkingDirectory,
            cancellationToken,
            "rev-parse",
            "HEAD^{tree}");

        _logger.LogInformation("Applying changes.");
        await definition.ApplyChanges(gitContext, cancellationToken);
        await gitContext.CommitAsync(definition.Title, cancellationToken);

        string treeHash = await _git.RunAsync(
            secret: null,
            workspace.WorkingDirectory,
            cancellationToken,
            "rev-parse",
            "HEAD^{tree}");

        PullRequestState desired = new(
            definition.Key,
            definition.Title,
            definition.Body,
            definition.TargetBranch,
            treeHash);

        TargetBranchState targetBranch = new(targetBranchTreeHash);

        IOperation[] operations = Planner.Plan(
            workspace.WorkingDirectory,
            identity,
            desired,
            targetBranch,
            existing,
            updateStrategy,
            onForeignCommits
        ).ToArray();

        if (operations.Length == 0)
        {
            _logger.LogInformation("Pull request already up to date; nothing to do.");
        }
        else
        {
            var operationsString = string.Join(", ", operations);
            _logger.LogInformation(
                "Planned {Count} operation(s) to reconcile the pull request: [ {Operations} ]",
                operations.Length,
                operationsString);
        }

        IReadOnlyList<IOperationResult> results = await host.ExecuteAsync(operations, cancellationToken);

        // A create result carries its own URL — trust it directly.
        PullRequestCreated? created = results.OfType<PullRequestCreated>().SingleOrDefault();
        if (created is not null)
        {
            return new PullRequestResult(PullRequestAction.Created, created.Url);
        }

        // No pull request was created. The URL, if any, is the one the host reported
        // alongside the existing pull request — null when none exists.
        PullRequestAction action = results.Count > 0 ? PullRequestAction.Updated : PullRequestAction.NoChange;
        return new PullRequestResult(action, existing?.Url);
    }

    private static IProcessRunner CreateDefaultProcessRunner(ILoggerFactory? loggerFactory)
    {
        ILoggerFactory effectiveLoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        return new ProcessRunner(effectiveLoggerFactory.CreateLogger<ProcessRunner>());
    }
}
