// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// An <see cref="IRepoHost"/> for repositories hosted on GitHub. Branches are
/// pushed with the git CLI; the GitHub API is only used to manage pull
/// requests and comments.
/// </summary>
public sealed class GitHubRepoHost : IRepoHost
{
    /// <param name="repo">The repository that pull requests merge into and branches are pushed to.</param>
    /// <param name="options">Common automation settings.</param>
    /// <param name="headRepo">
    /// The repository that pull request head branches are pushed to. Specify a
    /// fork here to avoid pushing branches to <paramref name="repo"/> itself.
    /// </param>
    public GitHubRepoHost(
        GitHubRepo repo,
        GitAutomationOptions options,
        GitHubRepo? headRepo = null,
        ILoggerFactory? loggerFactory = null)
    {
    }

    /// <inheritdoc/>
    public Task<EnsureResult> EnsurePullRequestAsync(PullRequestSpec spec, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<EnsureResult> EnsureBranchAsync(BranchSpec spec, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
