// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// The desired state of a branch: its current tip with the given changes
/// applied on top, committed directly to the branch (no pull request).
/// </summary>
public sealed record BranchSpec
{
    /// <summary>The branch to commit to.</summary>
    public required string Branch { get; init; }

    /// <summary>
    /// Applies the desired changes to a local clone of the repo and creates
    /// commits through the supplied <see cref="IGitContext"/>.
    /// </summary>
    public required Func<IGitContext, CancellationToken, Task> Apply { get; init; }
}
