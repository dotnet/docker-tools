// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Containers.ContainerRegistry;
using Azure.Core;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.DotNet.ImageBuilder;

public class RegistryCredentialsProvider(
    IAzureTokenCredentialProvider tokenCredentialProvider,
    IRegistryResolver registryResolver
) : IRegistryCredentialsProvider
{
    // Re-exchange a cached refresh token slightly before it expires so it isn't used past its lifetime.
    private static readonly TimeSpan s_refreshBuffer = TimeSpan.FromMinutes(5);

    // Cache refresh tokens per-registry to prevent rate limit/throttling errors from many parallel requests.
    private readonly ConcurrentDictionary<string, Lazy<Task<CachedRefreshToken>>> _refreshTokenCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Dynamically gets the RegistryCredentials for the specified registry in the following order of preference:
    ///     1. If we have authentication configured (e.g., service connection for ACR), use Azure SDK for authentication.
    ///     2. If we don't have authentication configured, try to read the username/password passed in from the command line.
    ///     3. Return null if there are no credentials to be found.
    /// </summary>
    /// <param name="registry">The container registry to get credentials for.</param>
    /// <returns>Registry credentials</returns>
    public async ValueTask<RegistryCredentials?> GetCredentialsAsync(
        string registry,
        IRegistryCredentialsHost? credsHost,
        CancellationToken ct = default)
    {
        RegistryInfo registryInfo = registryResolver.Resolve(registry, credsHost);

        if (registryInfo.RegistryAuthentication?.ServiceConnection is { } serviceConnection)
        {
            // If we're here, we have authentication configured with a service
            // connection we can use for authentication.
            Acr acr = Acr.Parse(registryInfo.EffectiveRegistry);
            CachedRefreshToken refreshToken = await GetRefreshTokenAsync(acr, serviceConnection, ct);

            // ACR login is an empty GUID as the username with the ACR refresh token as the password.
            return new RegistryCredentials(Guid.Empty.ToString(), refreshToken.RefreshToken);
        }

        // Fall back to credentials explicitly passed in via command line.
        return registryInfo.ExplicitCredentials;
    }

    private async Task<CachedRefreshToken> GetRefreshTokenAsync(
        Acr acr,
        IServiceConnection serviceConnection,
        CancellationToken ct)
    {
        Lazy<Task<CachedRefreshToken>> lazyRefreshTokenTask = _refreshTokenCache.GetOrAdd(
            acr.Server,
            _ => new Lazy<Task<CachedRefreshToken>>(() =>
                ExchangeAadTokenForAcrRefreshTokenAsync(acr, serviceConnection, ct)
            )
        );

        CachedRefreshToken refreshToken = await GetValueOrEvictCache(lazyRefreshTokenTask);

        // Re-exchange once when the cached token is at or near its expiration. The fresh token is
        // then used unconditionally, so a token that somehow reads as near-expiry can't cause an
        // exchange loop.
        if (refreshToken.ShouldRefresh(DateTimeOffset.UtcNow, s_refreshBuffer))
        {
            // Evict the stale entry (only if it's still the one we read) and exchange a fresh token.
            _refreshTokenCache.TryRemove(acr.Server, lazyRefreshTokenTask);

            Lazy<Task<CachedRefreshToken>> freshRefreshTokenExchange = _refreshTokenCache.GetOrAdd(
                acr.Server,
                _ => new Lazy<Task<CachedRefreshToken>>(() =>
                    ExchangeAadTokenForAcrRefreshTokenAsync(acr, serviceConnection, ct)
                )
            );
            refreshToken = await GetValueOrEvictCache(freshRefreshTokenExchange);
        }

        return refreshToken;

        async Task<CachedRefreshToken> GetValueOrEvictCache(Lazy<Task<CachedRefreshToken>> refreshTokenExchange)
        {
            try
            {
                return await refreshTokenExchange.Value;
            }
            catch
            {
                _refreshTokenCache.TryRemove(acr.Server, refreshTokenExchange);
                throw;
            }
        }
    }

    private async Task<CachedRefreshToken> ExchangeAadTokenForAcrRefreshTokenAsync(
        Acr acr,
        IServiceConnection serviceConnection,
        CancellationToken ct)
    {
        // Get AAD/Entra access token
        TokenCredential tokenCredential = tokenCredentialProvider.GetCredential(serviceConnection);
        var tokenRequestContext = new TokenRequestContext([AzureScopes.Default]);
        AccessToken accessToken = await tokenCredential.GetTokenAsync(tokenRequestContext, ct);

        // Exchange for ACR refresh token
        ContainerRegistryClient registryClient = new(acr.RegistryUri, tokenCredential);
        Response<AcrRefreshToken> response = await registryClient.ExchangeAadAccessTokenForAcrRefreshTokenAsync(
            service: acr.Server,
            tenant: serviceConnection.TenantId,
            refreshToken: null,
            accessToken: accessToken.Token,
            ct
        );

        return CachedRefreshToken.Create(response.Value.RefreshToken);
    }

    // An exchanged ACR refresh token paired with its expiration, parsed once when the token is obtained.
    private sealed record CachedRefreshToken(string RefreshToken, DateTimeOffset Expiration)
    {
        // Used when an ACR refresh token's expiration can't be read from its JWT payload.
        private static readonly TimeSpan s_fallbackLifetime = TimeSpan.FromMinutes(30);

        public static CachedRefreshToken Create(string refreshToken)
        {
            DateTimeOffset expiration = GetExpiration(refreshToken);
            return new CachedRefreshToken(refreshToken, expiration);
        }

        public bool ShouldRefresh(DateTimeOffset now, TimeSpan refreshBuffer) =>
            Expiration - now <= refreshBuffer;

        private static DateTimeOffset GetExpiration(string refreshToken)
        {
            try
            {
                JwtSecurityTokenHandler handler = new();
                if (handler.CanReadToken(refreshToken))
                {
                    JwtSecurityToken token = handler.ReadJwtToken(refreshToken);
                    return token.ValidTo;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or SecurityTokenMalformedException)
            {
            }

            // Fall back to sane default
            return DateTimeOffset.UtcNow + s_fallbackLifetime;
        }
    }
}
