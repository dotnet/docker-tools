// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Azure.Security.KeyVault.Keys.Cryptography;
using Octokit;

namespace Microsoft.DotNet.GitAutomation.GitHub;

/// <summary>
/// Provides GitHub App access using a private key stored in Azure Key Vault.
/// </summary>
public sealed class GitHubAppAccessProvider : IGitHubAccessProvider
{
    private static readonly ProductHeaderValue s_productHeaderValue = new("Microsoft.DotNet.GitAutomation");

    private readonly GitHubClient _appClient;
    private readonly ConcurrentDictionary<GitHubRepo, long> _repoToInstallationId = [];
    private readonly ConcurrentDictionary<long, GitHubAccess> _installationAccess = [];
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _installationAccessLock = [];
    private readonly SemaphoreSlim _identityLock = new(1, 1);
    private AutomationIdentity? _identity;

    /// <summary>
    /// Creates a GitHub App repository access provider.
    /// </summary>
    /// <param name="clientId">GitHub App client ID.</param>
    /// <param name="cryptographyClient">
    /// A client for an RSA key in Azure Key Vault. The caller's Azure identity must be allowed to sign with the key.
    /// </param>
    public GitHubAppAccessProvider(string clientId, CryptographyClient cryptographyClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(cryptographyClient);
        _appClient = new(s_productHeaderValue, new GitHubAppCredentialStore(clientId, cryptographyClient));
    }

    /// <inheritdoc/>
    public async ValueTask<GitHubAccess> GetAccessAsync(GitHubRepo repository, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_repoToInstallationId.TryGetValue(repository, out long installationId))
        {
            Installation installation = await _appClient.GitHubApps
                .GetRepositoryInstallationForCurrent(repository.Owner, repository.Name);

            cancellationToken.ThrowIfCancellationRequested();

            installationId = installation.Id;
            _repoToInstallationId[repository] = installationId;
        }

        // Double-checked locking
        if (_installationAccess.TryGetValue(installationId, out GitHubAccess? access))
        {
            return access;
        }

        // One GitHubAccess per App installation
        SemaphoreSlim accessLock = _installationAccessLock.GetOrAdd(installationId, _ => new(1, 1));
        await accessLock.WaitAsync(cancellationToken);

        try
        {
            if (_installationAccess.TryGetValue(installationId, out access))
            {
                return access;
            }

            var credentials = new GitHubAppInstallationCredentialStore(_appClient, installationId);
            var installationClient = new GitHubClient(s_productHeaderValue, credentials);

            AutomationIdentity identity = await GetIdentityAsync(installationClient, cancellationToken);

            access = new(credentials, identity, installationClient);
            _installationAccess[installationId] = access;
            return access;
        }
        finally
        {
            accessLock.Release();
        }
    }

    private async Task<AutomationIdentity> GetIdentityAsync(
        GitHubClient installationClient,
        CancellationToken cancellationToken)
    {
        // Double-checked locking
        if (_identity is null)
        {
            await _identityLock.WaitAsync(cancellationToken);
            try
            {
                if (_identity is null)
                {
                    GitHubApp app = await _appClient.GitHubApps.GetCurrent();
                    cancellationToken.ThrowIfCancellationRequested();

                    User bot = await installationClient.User.Get($"{app.Slug}[bot]");
                    cancellationToken.ThrowIfCancellationRequested();

                    _identity = new(bot.Login, $"{bot.Id}+{bot.Login}@users.noreply.github.com");
                }
            }
            finally
            {
                _identityLock.Release();
            }
        }

        return _identity;
    }
}
