// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation;

/// <summary>
/// Provides git operations available to a pull request definition while it applies changes.
/// </summary>
public interface IGitContext
{
    /// <summary>
    /// The local working directory where changes should be applied.
    /// </summary>
    string WorkspaceDirectory { get; }

    /// <summary>
    /// Commits the current workspace changes using the specified commit message.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="cancellationToken">A token that cancels the commit operation.</param>
    Task CommitAsync(string message, CancellationToken cancellationToken);
}
