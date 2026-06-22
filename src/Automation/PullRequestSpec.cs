// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

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

    /// <summary>
    /// The pull request title. Re-synced when the pull request is created or
    /// updated; left unchanged when the operation stops.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The pull request description. Re-synced when the pull request is created
    /// or updated; left unchanged when the operation stops.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>The branch that the pull request merges into.</summary>
    public required string TargetBranch { get; init; }

    /// <summary>
    /// Applies the desired changes to a local clone of the repo and creates
    /// commits through the supplied <see cref="IGitContext"/>.
    /// </summary>
    public required Func<IGitContext, CancellationToken, Task> Apply { get; init; }

    /// <summary>How re-runs update an existing pull request.</summary>
    public PullRequestUpdateStrategy UpdateStrategy { get; init; } = PullRequestUpdateStrategy.Append;

    /// <summary>
    /// What to do when the pull request's branch contains commits that were
    /// not authored by <see cref="GitAutomationOptions.Author"/> (e.g. another
    /// actor pushed a fix to the bot's branch).
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
