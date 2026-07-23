// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Octokit;

namespace Microsoft.DotNet.GitAutomation.GitHub;

internal sealed class GitHubAppInstallationCredentialStore(IGitHubClient appClient, long installationId)
    : ICredentialStore
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private AccessToken? _accessToken;

    public async Task<Credentials> GetCredentials()
    {
        if (_accessToken.IsValid())
        {
            return new(_accessToken.Token);
        }

        await _refreshLock.WaitAsync();
        try
        {
            if (!_accessToken.IsValid())
            {
                _accessToken = await appClient.GitHubApps.CreateInstallationToken(installationId);
            }

            return new(_accessToken.Token);
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
