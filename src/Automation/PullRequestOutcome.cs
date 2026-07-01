// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// What an <see cref="IRepoHost.EnsurePullRequestAsync"/> operation did.
/// </summary>
public enum PullRequestOutcome
{
    /// <summary>
    /// The pull request already contained the desired changes; nothing was
    /// pushed or modified.
    /// </summary>
    Unchanged,

    /// <summary>A new pull request was created.</summary>
    Created,

    /// <summary>An existing pull request's content or metadata was updated.</summary>
    Updated,

    /// <summary>
    /// The operation stopped without updating the pull request, per policy
    /// (e.g. foreign commits with
    /// <see cref="ForeignCommitPolicy.CommentAndStop"/>). See
    /// <see cref="PullRequestResult.Detail"/> for why.
    /// </summary>
    Stopped,

    /// <summary>
    /// This was a dry run: there were changes, but no pull request was created
    /// or modified.
    /// </summary>
    DryRun,
}
