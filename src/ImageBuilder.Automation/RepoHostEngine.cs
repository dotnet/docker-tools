// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// Service-agnostic implementation of the <see cref="IRepoHost"/>
/// reconciliation contract. All git operations are performed with the git CLI;
/// service-specific pull request operations are delegated to an
/// <see cref="IPullRequestApi"/>. The reference for its semantics is the
/// ModelRepoHost executable specification in the test project, which the
/// equivalence tests hold this engine to.
/// </summary>
/// <param name="targetRepo">The repository that pull requests merge into and branches are pushed to.</param>
/// <param name="headRepo">
/// The repository that pull request head branches are pushed to. Differs from
/// <paramref name="targetRepo"/> only when pull requests come from a fork.
/// </param>
internal sealed class RepoHostEngine(
    RemoteRepo targetRepo,
    RemoteRepo headRepo,
    IPullRequestApi pullRequests,
    GitAutomationOptions options,
    ILogger logger) : IRepoHost
{
    /// <inheritdoc/>
    public async Task<EnsureResult> EnsureBranchAsync(BranchSpec spec, CancellationToken cancellationToken = default)
    {
        GitRunner git = new(logger, options.Token);
        Uri url = targetRepo.GetAuthenticatedCloneUrl(options.Token);

        using GitWorkspace workspace = await GitWorkspace.CloneAsync(url, spec.Branch, options.Author, git, logger);

        await spec.Apply(workspace.Path);

        if (!await workspace.HasChangesAsync())
        {
            logger.LogInformation("Branch '{Branch}' already contains the desired changes.", spec.Branch);
            return new EnsureResult { Outcome = EnsureOutcome.Unchanged };
        }

        await workspace.LogChangesAsync();

        if (options.IsDryRun)
        {
            logger.LogInformation("Dry run: nothing was pushed to branch '{Branch}'.", spec.Branch);
            return new EnsureResult { Outcome = EnsureOutcome.DryRun };
        }

        string commitSha = await workspace.CommitAllAsync(spec.CommitMessage);
        await workspace.PushAsync(url, spec.Branch);

        logger.LogInformation("Pushed commit {CommitSha} to branch '{Branch}'.", commitSha, spec.Branch);
        return new EnsureResult { Outcome = EnsureOutcome.Updated, CommitSha = commitSha };
    }

    /// <inheritdoc/>
    public async Task<EnsureResult> EnsurePullRequestAsync(
        PullRequestSpec spec,
        CancellationToken cancellationToken = default)
    {
        GitRunner git = new(logger, options.Token);
        Uri targetUrl = targetRepo.GetAuthenticatedCloneUrl(options.Token);
        Uri headUrl = headRepo.GetAuthenticatedCloneUrl(options.Token);

        using GitWorkspace workspace =
            await GitWorkspace.CloneAsync(targetUrl, spec.TargetBranch, options.Author, git, logger);
        string targetSha = await workspace.RevParseAsync("HEAD");

        PullRequestInfo? pullRequest = await pullRequests.FindOpenAsync(spec.Key, spec.TargetBranch, cancellationToken);
        return pullRequest is null
            ? await CreateAsync(spec, workspace, headUrl, targetSha, cancellationToken)
            : await UpdateAsync(spec, pullRequest, workspace, headUrl, targetSha, cancellationToken);
    }

    private async Task<EnsureResult> CreateAsync(
        PullRequestSpec spec,
        GitWorkspace workspace,
        Uri headUrl,
        string targetSha,
        CancellationToken cancellationToken)
    {
        await workspace.CheckoutNewBranchAsync(spec.Key);
        await spec.Apply(workspace.Path);

        if (!await workspace.HasChangesAsync())
        {
            logger.LogInformation(
                "Branch '{TargetBranch}' already contains the changes for pull request '{Key}'.",
                spec.TargetBranch,
                spec.Key);
            return new EnsureResult { Outcome = EnsureOutcome.Unchanged };
        }

        await workspace.LogChangesAsync();

        // Creating the pull request force-recreates the head branch, so a
        // stale head branch (e.g. from a closed pull request) carrying foreign
        // commits blocks creation under CommentAndStop — "never destroy a
        // human's work" holds on the create path too.
        if (spec.OnForeignCommits == ForeignCommitPolicy.CommentAndStop
            && await workspace.RemoteBranchExistsAsync(headUrl, spec.Key))
        {
            await workspace.FetchAsync(headUrl, spec.Key);
            string staleHeadSha = await workspace.RevParseAsync("FETCH_HEAD");
            IReadOnlyList<GitCommit> foreignCommits =
                await GetForeignCommitsAsync(workspace, staleHeadSha, targetSha);

            if (foreignCommits.Count > 0)
            {
                string detail = StopMessage(foreignCommits, spec.StopComment);
                logger.LogWarning("{Detail}", detail);
                return new EnsureResult { Outcome = EnsureOutcome.Stopped, Detail = detail };
            }
        }

        if (options.IsDryRun)
        {
            logger.LogInformation("Dry run: no pull request was created for '{Key}'.", spec.Key);
            return new EnsureResult { Outcome = EnsureOutcome.DryRun };
        }

        string commitSha = await workspace.CommitAllAsync(spec.CommitMessage);
        await workspace.PushAsync(headUrl, spec.Key, force: true);

        PullRequestInfo created =
            await pullRequests.CreateAsync(spec.Title, spec.Body, spec.Key, spec.TargetBranch, cancellationToken);

        logger.LogInformation("Created pull request {Url}", created.Url);
        return new EnsureResult { Outcome = EnsureOutcome.Created, Url = created.Url, CommitSha = commitSha };
    }

    private async Task<EnsureResult> UpdateAsync(
        PullRequestSpec spec,
        PullRequestInfo pullRequest,
        GitWorkspace workspace,
        Uri headUrl,
        string targetSha,
        CancellationToken cancellationToken)
    {
        await workspace.FetchAsync(headUrl, spec.Key);
        string headSha = await workspace.RevParseAsync("FETCH_HEAD");

        // With Append, changes are applied on top of the pull request's
        // existing commits; otherwise on top of the target branch.
        string baseSha = spec.UpdateStrategy == PullRequestUpdateStrategy.Append ? headSha : targetSha;
        await workspace.CheckoutNewBranchAsync(spec.Key, baseSha);

        await spec.Apply(workspace.Path);

        string desiredTreeSha = await workspace.StageAllAndGetTreeAsync();
        string headTreeSha = await workspace.RevParseAsync($"{headSha}^{{tree}}");
        bool contentChanged = desiredTreeSha != headTreeSha;
        bool metadataChanged = pullRequest.Title != spec.Title || pullRequest.Body != spec.Body;

        if (!contentChanged && !metadataChanged)
        {
            logger.LogInformation("Pull request {Url} is already up to date.", pullRequest.Url);
            return new EnsureResult { Outcome = EnsureOutcome.Unchanged, Url = pullRequest.Url };
        }

        if (contentChanged)
        {
            await workspace.LogChangesAsync();

            IReadOnlyList<GitCommit> foreignCommits = await GetForeignCommitsAsync(workspace, headSha, targetSha);
            if (foreignCommits.Count > 0 && spec.OnForeignCommits == ForeignCommitPolicy.CommentAndStop)
            {
                string comment = StopMessage(foreignCommits, spec.StopComment);
                logger.LogWarning("{Detail}", comment);

                // Post the explanation at most once so scheduled re-runs don't
                // spam the pull request.
                if (!options.IsDryRun
                    && !(await pullRequests.GetCommentsAsync(pullRequest.Id, cancellationToken)).Contains(comment))
                {
                    await pullRequests.AddCommentAsync(pullRequest.Id, comment, cancellationToken);
                }

                return new EnsureResult { Outcome = EnsureOutcome.Stopped, Url = pullRequest.Url, Detail = comment };
            }
        }

        if (options.IsDryRun)
        {
            logger.LogInformation("Dry run: pull request {Url} was not updated.", pullRequest.Url);
            return new EnsureResult { Outcome = EnsureOutcome.DryRun, Url = pullRequest.Url };
        }

        if (metadataChanged)
        {
            await pullRequests.UpdateAsync(pullRequest.Id, spec.Title, spec.Body, cancellationToken);
        }

        if (!contentChanged)
        {
            logger.LogInformation("Updated title/description of pull request {Url}.", pullRequest.Url);
            return new EnsureResult { Outcome = EnsureOutcome.Updated, Url = pullRequest.Url };
        }

        string commitSha = await workspace.CommitAllAsync(spec.CommitMessage);
        await workspace.PushAsync(headUrl, spec.Key, force: spec.UpdateStrategy == PullRequestUpdateStrategy.Replace);

        logger.LogInformation("Updated pull request {Url}", pullRequest.Url);
        return new EnsureResult { Outcome = EnsureOutcome.Updated, Url = pullRequest.Url, CommitSha = commitSha };
    }

    /// <summary>
    /// Commits on the head branch that are not reachable from the target
    /// branch and were not authored by the automation.
    /// </summary>
    private async Task<IReadOnlyList<GitCommit>> GetForeignCommitsAsync(
        GitWorkspace workspace,
        string headSha,
        string targetSha) =>
        [
            .. (await workspace.GetCommitsAsync(headSha, targetSha))
                .Where(c => c.AuthorName != options.Author.Name || c.AuthorEmail != options.Author.Email),
        ];

    private static string StopMessage(IReadOnlyList<GitCommit> foreignCommits, string? stopComment)
    {
        string commits = string.Join(
            Environment.NewLine,
            foreignCommits.Select(c => $"- {c.Sha}: {c.Subject} ({c.AuthorName})"));

        string message =
            $"This pull request was not updated automatically because its branch contains commits "
            + $"that were not authored by the automation:{Environment.NewLine}{commits}";

        return stopComment is null ? message : $"{message}{Environment.NewLine}{Environment.NewLine}{stopComment}";
    }
}
