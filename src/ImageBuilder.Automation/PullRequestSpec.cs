// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// The desired state of an automated pull request.
/// </summary>
public sealed record PullRequestSpec
{
    /// <summary>
    /// A stable identifier for the pull request. It is used as the name of the
    /// pull request's head branch, which is how the pull request is found
    /// again on subsequent runs. Use the same key to update an existing pull
    /// request; use a different key to open a separate pull request.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>The pull request title. Re-synced on every run.</summary>
    public required string Title { get; init; }

    /// <summary>The pull request description. Re-synced on every run.</summary>
    public required string Body { get; init; }

    /// <summary>The message used for commits pushed to the head branch.</summary>
    public required string CommitMessage { get; init; }

    /// <summary>The branch that the pull request merges into.</summary>
    public required string TargetBranch { get; init; }

    /// <summary>Produces the desired changes in a local clone of the repo.</summary>
    public required ApplyChanges Apply { get; init; }

    /// <summary>How re-runs update an existing pull request.</summary>
    public PullRequestUpdateStrategy UpdateStrategy { get; init; } = PullRequestUpdateStrategy.Replace;

    /// <summary>
    /// What to do when the pull request's branch contains commits that were
    /// not authored by <see cref="GitAutomationOptions.Author"/> (e.g. a human
    /// pushed a fix to the bot's branch).
    /// </summary>
    public ForeignCommitPolicy OnForeignCommits { get; init; } = ForeignCommitPolicy.CommentAndStop;

    /// <summary>
    /// Additional text appended to the comment posted when
    /// <see cref="ForeignCommitPolicy.CommentAndStop"/> stops an update, e.g.
    /// instructions for applying the update manually. The comment always
    /// lists the foreign commits that were found.
    /// </summary>
    public string? StopComment { get; init; }
}

/// <summary>
/// Controls what happens when changes are pushed for a pull request that
/// already exists.
/// </summary>
public enum PullRequestUpdateStrategy
{
    /// <summary>
    /// Reset the pull request to contain exactly the new changes on top of the
    /// target branch (force push). If the resulting content is identical to
    /// what the pull request already contains, nothing is pushed.
    /// </summary>
    Replace,

    /// <summary>
    /// Add the new changes as an additional commit on top of the pull
    /// request's existing commits. If the changes are already present in the
    /// pull request, nothing is pushed.
    /// </summary>
    Append,
}

/// <summary>
/// What to do when a pull request's branch contains commits from someone
/// other than the automation. Detection is mechanical (commits whose author
/// differs from <see cref="GitAutomationOptions.Author"/>); the decision is
/// the caller's.
/// </summary>
public enum ForeignCommitPolicy
{
    /// <summary>
    /// Post a comment on the pull request explaining why the update was
    /// skipped (including <see cref="PullRequestSpec.StopComment"/>, if set)
    /// and stop without modifying the branch. The safe default: the
    /// automation never destroys a human's work.
    /// </summary>
    CommentAndStop,

    /// <summary>
    /// Proceed as if the foreign commits were the automation's own, applying
    /// <see cref="PullRequestSpec.UpdateStrategy"/> normally. With
    /// <see cref="PullRequestUpdateStrategy.Replace"/> this discards the
    /// foreign commits.
    /// </summary>
    Overwrite,
}
