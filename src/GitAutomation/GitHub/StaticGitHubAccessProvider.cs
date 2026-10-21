// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Octokit;
using Octokit.Internal;

namespace Microsoft.DotNet.GitAutomation.GitHub;

/// <summary>
/// Provides fixed credentials for GitHub repositories.
/// </summary>
/// <param name="token">The access token value.</param>
/// <param name="identity">The git identity represented by the token.</param>
public sealed class StaticGitHubAccessProvider(string token, AutomationIdentity identity) : IGitHubAccessProvider
{
    private static readonly ProductHeaderValue ProductHeaderValue = new("Microsoft.DotNet.GitAutomation");
    private readonly GitHubAccess _access = CreateAccess(token, identity);

    /// <inheritdoc/>
    public ValueTask<GitHubAccess> GetAccessAsync(GitHubRepo repository, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_access);

    private static GitHubAccess CreateAccess(string token, AutomationIdentity identity)
    {
        InMemoryCredentialStore credentials = new InMemoryCredentialStore(new Credentials(token));
        return new(credentials, identity, new GitHubClient(ProductHeaderValue, credentials));
    }
}
