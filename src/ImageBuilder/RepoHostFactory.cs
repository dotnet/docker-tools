// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Automation;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder;

internal class RepoHostFactory(
    IOctokitClientFactory octokitClientFactory,
    ILoggerFactory loggerFactory)
    : IRepoHostFactory
{
    private readonly IOctokitClientFactory _octokitClientFactory = octokitClientFactory
        ?? throw new ArgumentNullException(nameof(octokitClientFactory));
    private readonly ILoggerFactory _loggerFactory = loggerFactory
        ?? throw new ArgumentNullException(nameof(loggerFactory));

    public async Task<IRepoHost> CreateRepoHostAsync(GitOptions gitOptions, bool isDryRun)
    {
        string token = await _octokitClientFactory.CreateGitHubTokenAsync(gitOptions.GitHubAuthOptions);

        return new GitHubRepoHost(
            new GitHubRepo(gitOptions.Owner, gitOptions.Repo),
            new GitAutomationOptions(
                token,
                new GitAuthor(gitOptions.Username, gitOptions.Email),
                isDryRun),
            loggerFactory: _loggerFactory);
    }
}
