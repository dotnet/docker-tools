// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// A repository on a hosting service (GitHub, Azure DevOps), exposed as a
/// reconciliation target: callers declare the desired state of a pull request
/// or branch, and the host makes reality match it. All operations are
/// idempotent — ensuring a state that already exists is a no-op.
/// </summary>
public interface IRepoHost
{
    /// <summary>
    /// Ensures that an open pull request exists matching <paramref name="spec"/>.
    /// The pull request is identified by <see cref="PullRequestSpec.Key"/>:
    /// <list type="bullet">
    /// <item>No open pull request with the key → create the branch, apply the
    /// changes, and open the pull request.</item>
    /// <item>An open pull request already contains the desired changes →
    /// no-op.</item>
    /// <item>An open pull request exists with different content → update it
    /// according to <see cref="PullRequestSpec.UpdateStrategy"/> and
    /// <see cref="PullRequestSpec.OnForeignCommits"/>.</item>
    /// </list>
    /// </summary>
    Task<PullRequestResult> EnsurePullRequestAsync(PullRequestSpec spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that the tip of a branch contains the changes described by
    /// <paramref name="spec"/>, committing and pushing them directly if they
    /// are not already present. The push is fast-forward only.
    /// </summary>
    Task<BranchResult> EnsureBranchContentAsync(BranchSpec spec, CancellationToken cancellationToken = default);
}
