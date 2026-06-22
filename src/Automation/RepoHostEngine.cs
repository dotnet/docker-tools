// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Service-agnostic implementation of the <see cref="IRepoHost"/>
/// reconciliation contract. All git operations are performed with the git CLI;
/// service-specific pull request operations are delegated to an
/// <see cref="IPullRequestApi"/>.
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
    public async Task<BranchResult> EnsureBranchContentAsync(
        BranchSpec spec,
        CancellationToken cancellationToken = default)
    {
        GitCli git = new(logger, options.Token);
        Uri url = targetRepo.GetAuthenticatedCloneUrl(options.Token);

        using GitWorkspace workspace = await GitWorkspace.CloneAsync(url, spec.Branch, options.Author, git, logger);

        GitContext context = await ApplyAsync(spec.Apply, workspace, cancellationToken);

        if (context.Commits.Count == 0)
        {
            logger.LogInformation("Branch '{Branch}' already contains the desired changes.", spec.Branch);
            return new BranchResult { Outcome = BranchOutcome.Unchanged };
        }

        if (options.IsDryRun)
        {
            logger.LogInformation("Dry run: nothing was pushed to branch '{Branch}'.", spec.Branch);
            return new BranchResult { Outcome = BranchOutcome.DryRun };
        }

        await workspace.PushAsync(url, spec.Branch);

        logger.LogInformation(
            "Pushed commits {CommitShas} to branch '{Branch}'.",
            string.Join(", ", context.Commits.Select(commit => commit.Sha)),
            spec.Branch);
        return new BranchResult { Outcome = BranchOutcome.Updated, Commits = context.Commits };
    }

    /// <inheritdoc/>
    public async Task<PullRequestResult> EnsurePullRequestAsync(
        PullRequestSpec spec,
        CancellationToken cancellationToken = default)
    {
        GitCli git = new(logger, options.Token);
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

    private async Task<PullRequestResult> CreateAsync(
        PullRequestSpec spec,
        GitWorkspace workspace,
        Uri headUrl,
        string targetSha,
        CancellationToken cancellationToken)
    {
        await workspace.CheckoutNewBranchAsync(spec.Key);
        GitContext context = await ApplyAsync(spec.Apply, workspace, cancellationToken);

        if (context.Commits.Count == 0)
        {
            logger.LogInformation(
                "No commits were produced for pull request '{Key}' targeting branch '{TargetBranch}'.",
                spec.Key,
                spec.TargetBranch);

            return new PullRequestResult { Outcome = PullRequestOutcome.Unchanged };
        }

        // Creating the pull request force-recreates the head branch, so a
        // stale head branch (e.g. from a closed pull request) carrying foreign
        // commits blocks creation under CommentAndStop — "never destroy another
        // actor's work" holds on the create path too.
        if (spec.OnForeignCommits == ForeignCommitPolicy.CommentAndStop
            && await workspace.RemoteBranchExistsAsync(headUrl, spec.Key))
        {
            await workspace.FetchAsync(headUrl, spec.Key);
            string staleHeadSha = await workspace.RevParseAsync("FETCH_HEAD");
            IReadOnlyList<GitCommit> foreignCommits = await GetForeignCommitsAsync(workspace, staleHeadSha, targetSha);

            if (foreignCommits.Count > 0)
            {
                string detail = StopMessage(foreignCommits, spec.StopComment);
                logger.LogWarning("{Detail}", detail);
                return new PullRequestResult
                {
                    Outcome = PullRequestOutcome.Stopped,
                    Detail = detail
                };
            }
        }

        if (options.IsDryRun)
        {
            logger.LogInformation("Dry run: no pull request was created for '{Key}'.", spec.Key);
            return new PullRequestResult { Outcome = PullRequestOutcome.DryRun };
        }

        await workspace.PushAsync(headUrl, spec.Key, force: true);

        PullRequestInfo created = await pullRequests.CreateAsync(
            title: spec.Title,
            body: spec.Body,
            headBranch: spec.Key,
            targetBranch: spec.TargetBranch,
            cancellationToken: cancellationToken);

        logger.LogInformation("Created pull request {Url}", created.Url);
        return new PullRequestResult
        {
            Outcome = PullRequestOutcome.Created,
            Url = created.Url,
            Commits = context.Commits
        };
    }

    private async Task<PullRequestResult> UpdateAsync(
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

        GitContext context = await ApplyAsync(spec.Apply, workspace, cancellationToken);

        string desiredTreeSha = await workspace.RevParseAsync("HEAD^{tree}");
        string headTreeSha = await workspace.RevParseAsync($"{headSha}^{{tree}}");
        bool contentChanged = context.Commits.Count > 0 && desiredTreeSha != headTreeSha;
        bool metadataChanged = pullRequest.Title != spec.Title || pullRequest.Body != spec.Body;

        if (!contentChanged && !metadataChanged)
        {
            logger.LogInformation("Pull request {Url} is already up to date.", pullRequest.Url);
            return new PullRequestResult { Outcome = PullRequestOutcome.Unchanged, Url = pullRequest.Url };
        }

        if (contentChanged)
        {
            IReadOnlyList<GitCommit> foreignCommits = await GetForeignCommitsAsync(workspace, headSha, targetSha);
            if (foreignCommits.Count > 0 && spec.OnForeignCommits == ForeignCommitPolicy.CommentAndStop)
            {
                string comment = StopMessage(foreignCommits, spec.StopComment);
                logger.LogWarning("{Detail}", comment);

                // Post the explanation at most once so scheduled re-runs don't spam the pull request.
                if (!options.IsDryRun
                    && !(await pullRequests.GetCommentsAsync(pullRequest.Id, cancellationToken)).Contains(comment))
                {
                    await pullRequests.AddCommentAsync(pullRequest.Id, comment, cancellationToken);
                }

                return new PullRequestResult
                {
                    Outcome = PullRequestOutcome.Stopped,
                    Url = pullRequest.Url,
                    Detail = comment
                };
            }
        }

        if (options.IsDryRun)
        {
            logger.LogInformation("Dry run: pull request {Url} was not updated.", pullRequest.Url);
            return new PullRequestResult
            {
                Outcome = PullRequestOutcome.DryRun,
                Url = pullRequest.Url
            };
        }

        if (metadataChanged)
        {
            await pullRequests.UpdateAsync(pullRequest.Id, spec.Title, spec.Body, cancellationToken);
        }

        if (!contentChanged)
        {
            logger.LogInformation("Updated title/description of pull request {Url}.", pullRequest.Url);
            return new PullRequestResult
            {
                Outcome = PullRequestOutcome.Updated,
                Url = pullRequest.Url
            };
        }

        await workspace.PushAsync(headUrl, spec.Key, force: spec.UpdateStrategy == PullRequestUpdateStrategy.Replace);

        logger.LogInformation("Updated pull request {Url}", pullRequest.Url);
        return new PullRequestResult
        {
            Outcome = PullRequestOutcome.Updated,
            Url = pullRequest.Url,
            Commits = context.Commits
        };
    }

    private async Task<GitContext> ApplyAsync(
        Func<IGitContext, CancellationToken, Task> apply,
        GitWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var context = new GitContext(workspace, options.Author, logger);
        await apply(context, cancellationToken);
        await context.ThrowIfPendingChangesAsync();
        return context;
    }

    /// <summary>
    /// Commits on the head branch that are not reachable from the target
    /// branch and were not authored by the automation.
    /// </summary>
    private async Task<IReadOnlyList<GitCommit>> GetForeignCommitsAsync(
        GitWorkspace workspace,
        string headSha,
        string targetSha)
    {
        var commits = await workspace.GetCommitsAsync(headSha, targetSha);

        var foreignCommits = commits
            .Where(commit => commit.AuthorName != options.Author.Name || commit.AuthorEmail != options.Author.Email)
            .ToList();

        return foreignCommits;
    }

    private static string StopMessage(IReadOnlyList<GitCommit> foreignCommits, string? stopComment)
    {
        string commits = string.Join(
            Environment.NewLine,
            foreignCommits.Select(commit => $"- {commit.Sha}: {commit.Subject()} ({commit.AuthorName})"));

        string message =
            $"""
            This pull request was not updated automatically because its branch contains commits that were not authored by automation:

            {commits}

            """;

        return stopComment is null ? message : $"{message}{Environment.NewLine}{Environment.NewLine}{stopComment}";
    }
}
