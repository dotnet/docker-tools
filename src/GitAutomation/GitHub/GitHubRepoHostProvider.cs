// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.GitAutomation.GitHub;

internal sealed class GitHubRepoHostProvider(
    IGitHubAccessProvider accessProvider,
    ILoggerFactory loggerFactory,
    Git git) : IRepoHostProvider<GitHubRepo>
{
    public async ValueTask<IRepoHost> OpenAsync(
        GitHubRepo targetRepo,
        GitHubRepo sourceRepo,
        CancellationToken cancellationToken)
    {
        GitHubAccess access = await accessProvider.GetAccessAsync(targetRepo, cancellationToken);
        return new GitHubRepoHost(targetRepo, sourceRepo, access, loggerFactory, git);
    }
}
