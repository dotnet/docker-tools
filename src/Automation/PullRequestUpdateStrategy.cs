// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Controls what happens when changes are pushed for a pull request that
/// already exists.
/// </summary>
public enum PullRequestUpdateStrategy
{
    /// <summary>
    /// Add the new changes as an additional commit on top of the pull
    /// request's existing commits. If the changes are already present in the
    /// pull request, nothing is pushed. The safe default: existing history,
    /// including any commits another actor pushed, is preserved.
    /// </summary>
    Append,

    /// <summary>
    /// Reset the pull request to contain exactly the new changes on top of the
    /// target branch (force push). If the resulting content is identical to
    /// what the pull request already contains, nothing is pushed.
    /// </summary>
    Replace,
}
