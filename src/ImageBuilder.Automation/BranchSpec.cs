// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// The desired state of a branch: its current tip with the given changes
/// applied on top, committed directly to the branch (no pull request).
/// </summary>
public sealed record BranchSpec
{
    /// <summary>The branch to commit to.</summary>
    public required string Branch { get; init; }

    /// <summary>The message used for the commit, when one is needed.</summary>
    public required string CommitMessage { get; init; }

    /// <summary>Produces the desired changes in a local clone of the repo.</summary>
    public required ApplyChanges Apply { get; init; }
}
