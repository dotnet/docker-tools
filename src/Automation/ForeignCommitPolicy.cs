// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

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
    /// automation never destroys another actor's work.
    /// </summary>
    CommentAndStop,

    /// <summary>
    /// Proceed as if the foreign commits were the automation's own, applying
    /// <see cref="PullRequestUpdateStrategy"/> normally. With
    /// <see cref="PullRequestUpdateStrategy.Append"/> the foreign commits are
    /// kept and the update lands on top; only
    /// <see cref="PullRequestUpdateStrategy.Replace"/> discards them.
    /// </summary>
    Proceed,
}
