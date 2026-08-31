// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.GitAutomation;

/// <summary>
/// Creates or updates pull requests by applying changes in a temporary git workspace and
/// reconciling the resulting branch and pull request properties.
/// </summary>
public sealed class PullRequestManager
{
    private readonly IProcessRunner _processRunner;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PullRequestManager> _logger;

    /// <summary>Creates a manager with caller-provided process and logging services.</summary>
    /// <param name="processRunner">Runs git processes.</param>
    /// <param name="loggerFactory">Creates loggers.</param>
    public PullRequestManager(IProcessRunner processRunner, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PullRequestManager>();
        _processRunner = processRunner;
    }

    /// <summary>Creates a manager with the default process runner.</summary>
    /// <param name="loggerFactory">Creates loggers, or <see langword="null"/> to disable logging.</param>
    public PullRequestManager(ILoggerFactory? loggerFactory = null)
        : this(
            new ProcessRunner((loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ProcessRunner>()),
            loggerFactory ?? NullLoggerFactory.Instance)
    {
    }

    /// <summary>Creates or updates a pull request to match a definition.</summary>
    /// <param name="definition">The desired pull request state and workspace changes.</param>
    /// <param name="endpoint">The host-specific pull request endpoint.</param>
    /// <param name="updateStrategy">How an existing source branch is updated.</param>
    /// <param name="onForeignCommits">How commits from other authors are handled.</param>
    /// <param name="cancellationToken">A token that cancels reconciliation.</param>
    /// <returns>The action taken and pull request URL, when available.</returns>
    public async Task<PullRequestResult> CreateOrUpdateAsync(
        PullRequestDefinition definition,
        IPullRequestEndpoint endpoint,
        PullRequestUpdateStrategy updateStrategy = PullRequestUpdateStrategy.Append,
        ForeignCommitPolicy onForeignCommits = ForeignCommitPolicy.Proceed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(endpoint);

        AutomationIdentity identity = await endpoint.GetIdentityAsync(cancellationToken);
        ExistingPullRequest? existing = await endpoint.FindPullRequestAsync(definition.Key, cancellationToken);
        LogExistingPullRequest(existing, definition.Key);

        // Append builds on the existing source branch; other operations build from the target branch.
        bool appendExisting = existing is not null && updateStrategy == PullRequestUpdateStrategy.Append;
        string cloneBranch = appendExisting ? definition.Key : definition.TargetBranch;

        _logger.LogInformation("Cloning branch '{Branch}'.", cloneBranch);
        using GitWorkingCopy workingCopy = new GitWorkingCopy(
            _processRunner,
            _loggerFactory.CreateLogger<GitWorkingCopy>());

        if (appendExisting)
        {
            await endpoint.CloneSourceAsync(workingCopy, cloneBranch, identity, cancellationToken);
        }
        else
        {
            await endpoint.CloneTargetAsync(workingCopy, cloneBranch, identity, cancellationToken);
        }

        // When no pull request exists, this cloned target branch is the planner's baseline.
        string initialTreeHash = await workingCopy.GetTreeHashAsync(cancellationToken);

        await definition.ApplyChanges(workingCopy, cancellationToken);
        await workingCopy.CommitAsync(definition.Title, cancellationToken);

        string treeHash = await workingCopy.GetTreeHashAsync(cancellationToken);

        PullRequestState desired = new PullRequestState(
            definition.Key, definition.Title, definition.Body, definition.TargetBranch, treeHash);

        IOperation[] operations = Planner.Plan(
            identity,
            desired,
            new TargetBranchState(initialTreeHash),
            existing,
            updateStrategy,
            onForeignCommits).ToArray();

        LogPlannedOperations(operations);

        if (operations.Length == 0)
        {
            return new PullRequestResult(PullRequestAction.NoChange, existing?.Url);
        }

        Uri? pullRequestUrl = existing?.Url;
        int? pullRequestNumber = null;
        PullRequestChanges changes = new PullRequestChanges();

        foreach (IOperation operation in operations)
        {
            switch (operation)
            {
                case PushCommitsOperation push:
                    await PushAsync(endpoint, workingCopy, push, cancellationToken);
                    break;

                case CreatePullRequestOperation create:
                    NewPullRequest pullRequest = new NewPullRequest(
                        create.Title,
                        create.Body,
                        create.SourceBranch,
                        create.TargetBranch);

                    pullRequestUrl = await endpoint.CreatePullRequestAsync(pullRequest, cancellationToken);
                    break;

                case UpdateTitleOperation update:
                    changes = changes with { Title = update.Title };
                    pullRequestNumber = update.Number;
                    break;

                case UpdateBodyOperation update:
                    changes = changes with { Body = update.Body };
                    pullRequestNumber = update.Number;
                    break;

                case UpdateBaseBranchOperation update:
                    changes = changes with { TargetBranch = update.TargetBranch };
                    pullRequestNumber = update.Number;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown operation type '{operation.GetType()}'.");
            }
        }

        if (pullRequestNumber is int number)
        {
            await endpoint.UpdatePullRequestAsync(number, changes, cancellationToken);
        }

        PullRequestAction action = existing is null
            ? PullRequestAction.Created
            : PullRequestAction.Updated;

        return new PullRequestResult(action, pullRequestUrl);
    }

    private async Task PushAsync(
        IPullRequestEndpoint endpoint,
        GitWorkingCopy workingCopy,
        PushCommitsOperation operation,
        CancellationToken cancellationToken)
    {
        string sha = await workingCopy.GetHeadCommitAsync(cancellationToken);

        _logger.LogInformation(
            "Pushing commit {Sha} to branch '{Branch}'{Force}.",
            sha,
            operation.SourceBranch,
            operation.ForcePush ? " (force)" : string.Empty);

        await endpoint.PushAsync(
            workingCopy,
            operation.SourceBranch,
            operation.ForcePush,
            cancellationToken);

        _logger.LogInformation("Pushed commit {Sha}.", sha);
    }

    private void LogExistingPullRequest(ExistingPullRequest? existing, string key)
    {
        if (existing is null)
        {
            _logger.LogInformation("No open pull request found for branch '{Key}'.", key);
            return;
        }

        _logger.LogInformation(
            "Found open pull request #{Number} ({Url}) with {CommitCount} commit(s).",
            existing.Number, existing.Url, existing.Commits.Count);
    }

    private void LogPlannedOperations(IOperation[] operations)
    {
        if (operations.Length == 0)
        {
            _logger.LogInformation("Pull request already up to date; nothing to do.");
            return;
        }

        _logger.LogInformation(
            "Planned {Count} operation(s): [ {Operations} ]",
            operations.Length,
            string.Join(", ", operations.AsEnumerable()));
    }
}
