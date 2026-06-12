// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

/// <summary>
/// A reference implementation (executable specification) of the
/// <see cref="IRepoHost"/> contract against an in-memory <see cref="ModelRepo"/>.
/// The property tests run against this model today; once the real hosts
/// (<see cref="GitHubRepoHost"/>, <see cref="AzdoRepoHost"/>) are implemented,
/// they can be tested for equivalence against this model on generated
/// scenarios, transferring every property to the real implementations.
///
/// Spec decisions this model pins down that the doc comments leave open:
///
/// <list type="bullet">
///   <item>
///     The desired-content check runs before the foreign-commit check, so a
///     pull request that already contains the desired changes is Unchanged even
///     if a human has pushed to it — no comment is posted unless an update was
///     actually blocked.
///   </item>
///   <item>
///     A blocked update posts its explanatory comment at most once: posting
///     is skipped if an identical comment already exists, so scheduled re-runs do
///     not spam the pull request.
///   </item>
///   <item>
///     Creating a pull request force-recreates its head branch. If a stale
///     head branch (e.g. from a closed pull request) contains foreign commits,
///     <see cref="ForeignCommitPolicy.CommentAndStop"/> blocks creation too —
///     "never destroy a human's work" must hold on the create path as well.
///   </item>
///   <item>
///     Title and body are re-synced only when the pull request is created or
///     updated; a metadata-only difference yields Updated with a null
///     <see cref="EnsureResult.CommitSha"/>. <see cref="ForeignCommitPolicy.CommentAndStop"/>
///   blocks metadata changes too, so Stopped means the pull request was not updated.
///   </item>
///   <item>
///     With <see cref="PullRequestUpdateStrategy.Append"/>, the changes are
///     applied to a checkout of the pull request's current head branch; in every
///     other case they are applied to a checkout of the target branch.
///   </item>
/// </list>
///
/// </summary>
internal sealed class ModelRepoHost(ModelRepo repo, GitAutomationOptions options) : IRepoHost
{
    public ModelRepo Repo { get; } = repo;

    public async Task<EnsureResult> EnsureBranchAsync(BranchSpec spec, CancellationToken cancellationToken = default)
    {
        if (!Repo.Branches.TryGetValue(spec.Branch, out ModelCommit? tip))
            throw new GitException(
                command: $"git clone --branch {spec.Branch}",
                exitCode: 128,
                standardError: $"fatal: Remote branch {spec.Branch} not found");

        ImmutableDictionary<string, string> desiredTree = await ApplyToTreeAsync(tip.Tree, spec.Apply);

        if (FsTree.TreesEqual(desiredTree, tip.Tree))
            return new EnsureResult { Outcome = EnsureOutcome.Unchanged };

        if (options.IsDryRun)
            return new EnsureResult { Outcome = EnsureOutcome.DryRun };

        ModelCommit commit = Repo.Push(spec.Branch, options.Author, spec.CommitMessage, desiredTree);
        return new EnsureResult { Outcome = EnsureOutcome.Updated, CommitSha = commit.Sha };
    }

    public async Task<EnsureResult> EnsurePullRequestAsync(
        PullRequestSpec spec,
        CancellationToken cancellationToken = default
    )
    {
        if (!Repo.Branches.TryGetValue(spec.TargetBranch, out ModelCommit? targetTip))
            throw new GitException(
                command: $"git clone --branch {spec.TargetBranch}",
                exitCode: 128,
                standardError: $"fatal: Remote branch {spec.TargetBranch} not found");

        ModelPullRequest? pullRequest = Repo.FindOpenPullRequest(spec.Key);
        return pullRequest is null
            ? await CreateAsync(spec, targetTip)
            : await UpdateAsync(spec, pullRequest, targetTip);
    }

    private async Task<EnsureResult> CreateAsync(PullRequestSpec spec, ModelCommit targetTip)
    {
        ImmutableDictionary<string, string> desiredTree = await ApplyToTreeAsync(targetTip.Tree, spec.Apply);

        if (FsTree.TreesEqual(desiredTree, targetTip.Tree))
            return new EnsureResult { Outcome = EnsureOutcome.Unchanged };

        // Creating the pull request will force-recreate the head branch, so a
        // stale head branch carrying foreign commits blocks creation under
        // CommentAndStop, the same way it blocks updates.
        if (spec.OnForeignCommits == ForeignCommitPolicy.CommentAndStop && Repo.Branches.ContainsKey(spec.Key))
        {
            IReadOnlyList<ModelCommit> foreignCommits =
                Repo.ForeignCommits(spec.Key, spec.TargetBranch, options.Author);

            if (foreignCommits.Count > 0)
            {
                return new EnsureResult
                {
                    Outcome = EnsureOutcome.Stopped,
                    Detail = StopMessage(foreignCommits, spec.StopComment),
                };
            }
        }

        if (options.IsDryRun)
        {
            return new EnsureResult { Outcome = EnsureOutcome.DryRun };
        }

        var headTip = new ModelCommit
        {
            Author = options.Author,
            Message = spec.CommitMessage,
            Tree = desiredTree,
            Parents = [targetTip],
        };
        Repo.Branches[spec.Key] = headTip;

        var pullRequest = new ModelPullRequest
        {
            HeadBranch = spec.Key,
            TargetBranch = spec.TargetBranch,
            Url = $"https://model.test/pr/{spec.Key}/{Repo.PullRequests.Count(pr => pr.HeadBranch == spec.Key)}",
            Title = spec.Title,
            Body = spec.Body,
        };
        Repo.PullRequests.Add(pullRequest);

        return new EnsureResult
        {
            Outcome = EnsureOutcome.Created,
            Url = pullRequest.Url,
            CommitSha = headTip.Sha,
        };
    }

    private async Task<EnsureResult> UpdateAsync(
        PullRequestSpec spec,
        ModelPullRequest pullRequest,
        ModelCommit targetTip
    )
    {
        ModelCommit headTip = Repo.Branches[pullRequest.HeadBranch];
        ImmutableDictionary<string, string> baseTree =
            spec.UpdateStrategy == PullRequestUpdateStrategy.Append ? headTip.Tree : targetTip.Tree;
        ImmutableDictionary<string, string> desiredTree = await ApplyToTreeAsync(baseTree, spec.Apply);

        bool contentChanged = !FsTree.TreesEqual(desiredTree, headTip.Tree);
        bool metadataChanged = pullRequest.Title != spec.Title || pullRequest.Body != spec.Body;

        if (!contentChanged && !metadataChanged)
            return new EnsureResult { Outcome = EnsureOutcome.Unchanged, Url = pullRequest.Url };

        if (contentChanged)
        {
            IReadOnlyList<ModelCommit> foreignCommits =
                Repo.ForeignCommits(pullRequest.HeadBranch, spec.TargetBranch, options.Author);

            if (foreignCommits.Count > 0 && spec.OnForeignCommits == ForeignCommitPolicy.CommentAndStop)
            {
                string comment = StopMessage(foreignCommits, spec.StopComment);

                if (!options.IsDryRun && !pullRequest.Comments.Contains(comment))
                    pullRequest.Comments.Add(comment);

                return new EnsureResult
                {
                    Outcome = EnsureOutcome.Stopped,
                    Url = pullRequest.Url,
                    Detail = comment,
                };
            }
        }

        if (options.IsDryRun)
            return new EnsureResult { Outcome = EnsureOutcome.DryRun, Url = pullRequest.Url };

        pullRequest.Title = spec.Title;
        pullRequest.Body = spec.Body;

        if (!contentChanged)
            return new EnsureResult { Outcome = EnsureOutcome.Updated, Url = pullRequest.Url };

        ModelCommit parent = spec.UpdateStrategy == PullRequestUpdateStrategy.Append ? headTip : targetTip;
        var newTip = new ModelCommit
        {
            Author = options.Author,
            Message = spec.CommitMessage,
            Tree = desiredTree,
            Parents = [parent],
        };

        Repo.Branches[pullRequest.HeadBranch] = newTip;

        return new EnsureResult
        {
            Outcome = EnsureOutcome.Updated,
            Url = pullRequest.Url,
            CommitSha = newTip.Sha,
        };
    }

    private static string StopMessage(IReadOnlyList<ModelCommit> foreignCommits, string? stopComment)
    {
        string commits = string.Join(
            Environment.NewLine,
            foreignCommits.Select(c => $"- {c.Sha}: {c.Message} ({c.Author.Name})"));

        string message =
            $"This pull request was not updated automatically because its branch contains commits "
            + $"that were not authored by the automation:{Environment.NewLine}{commits}";

        return stopComment is null ? message : $"{message}{Environment.NewLine}{Environment.NewLine}{stopComment}";
    }

    /// <summary>
    /// Runs an <see cref="ApplyChanges"/> callback the way the library
    /// promises to: against a clean materialized checkout of the given tree.
    /// </summary>
    private static async Task<ImmutableDictionary<string, string>> ApplyToTreeAsync(
        ImmutableDictionary<string, string> tree,
        ApplyChanges apply
    )
    {
        string root = Path.Combine(Path.GetTempPath(), $"automation-model-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            FsTree.Write(root, tree);
            await apply(root);
            return FsTree.Read(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
