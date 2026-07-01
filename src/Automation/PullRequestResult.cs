// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// The result of an <see cref="IRepoHost.EnsurePullRequestAsync"/> operation:
/// what (if anything) had to change to reach the desired state.
/// </summary>
public sealed record PullRequestResult
{
    /// <summary>What the operation did.</summary>
    public required PullRequestOutcome Outcome { get; init; }

    /// <summary>A link to the pull request, when one exists.</summary>
    public string? Url { get; init; }

    /// <summary>The commits the automation pushed, in creation order.</summary>
    public IReadOnlyList<GitCommit> Commits { get; init; } = [];

    /// <summary>
    /// Human-readable explanation, e.g. why the operation stopped. Set when
    /// <see cref="Outcome"/> is <see cref="PullRequestOutcome.Stopped"/>.
    /// </summary>
    public string? Detail { get; init; }
}
