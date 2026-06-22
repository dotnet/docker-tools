// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// What an <see cref="IRepoHost.EnsureBranchContentAsync"/> operation did.
/// </summary>
public enum BranchOutcome
{
    /// <summary>
    /// The branch already contained the desired changes; nothing was pushed.
    /// </summary>
    Unchanged,

    /// <summary>New commits were pushed to the branch.</summary>
    Updated,

    /// <summary>
    /// This was a dry run: there were changes, but nothing was pushed.
    /// </summary>
    DryRun,
}
