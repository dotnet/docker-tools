// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// The result of an ensure operation: what (if anything) had to change to
/// reach the desired state.
/// </summary>
public sealed record EnsureResult
{
    /// <summary>What the operation did.</summary>
    public required EnsureOutcome Outcome { get; init; }

    /// <summary>A link to the pull request, when one exists.</summary>
    public string? Url { get; init; }

    /// <summary>The SHA of the pushed commit, when one was pushed.</summary>
    public string? CommitSha { get; init; }

    /// <summary>Human-readable explanation, e.g. why the operation stopped.</summary>
    public string? Detail { get; init; }
}

public enum EnsureOutcome
{
    /// <summary>
    /// The desired state already existed; nothing was pushed or modified.
    /// </summary>
    Unchanged,

    /// <summary>A new pull request was created.</summary>
    Created,

    /// <summary>An existing pull request or branch was updated.</summary>
    Updated,

    /// <summary>
    /// The operation stopped without modifying anything, per policy (e.g.
    /// foreign commits with <see cref="ForeignCommitPolicy.CommentAndStop"/>).
    /// See <see cref="EnsureResult.Detail"/> for why.
    /// </summary>
    Stopped,

    /// <summary>
    /// This was a dry run: there were changes, but nothing was pushed and no
    /// pull request was created or modified.
    /// </summary>
    DryRun,
}
