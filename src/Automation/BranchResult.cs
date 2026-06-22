// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// The result of an <see cref="IRepoHost.EnsureBranchContentAsync"/> operation:
/// what (if anything) had to change to reach the desired state.
/// </summary>
public sealed record BranchResult
{
    /// <summary>What the operation did.</summary>
    public required BranchOutcome Outcome { get; init; }

    /// <summary>The commits the automation pushed, in creation order.</summary>
    public IReadOnlyList<GitCommit> Commits { get; init; } = [];
}
