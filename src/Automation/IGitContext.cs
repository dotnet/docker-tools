// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Provides access to a temporary repository checkout while an automation
/// operation is producing commits.
/// </summary>
public interface IGitContext
{
    /// <summary>
    /// The root directory of the repository working tree.
    /// </summary>
    string Directory { get; }

    /// <summary>
    /// Commits the current working tree changes, or returns null when there are
    /// no changes to commit. Empty commit messages are rejected.
    /// </summary>
    /// <returns>The new commit, or null when the commit was skipped.</returns>
    Task<GitCommit?> CommitAsync(string message, CancellationToken cancellationToken = default);
}
