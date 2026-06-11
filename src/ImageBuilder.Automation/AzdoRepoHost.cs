// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// An <see cref="IRepoHost"/> for repositories hosted on Azure DevOps.
/// Branches are pushed with the git CLI; the Azure DevOps REST API is only
/// used to manage pull requests and comments. Pull requests are always
/// created from a branch in the target repository (Azure DevOps forks are not
/// supported).
/// </summary>
public sealed class AzdoRepoHost : IRepoHost
{
    public AzdoRepoHost(
        AzdoRepo repo,
        GitAutomationOptions options,
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
