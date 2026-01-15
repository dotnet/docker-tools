// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public class RegistryCredentialsProvider(
    IHttpClientProvider httpClientProvider,
    IAzureTokenCredentialProvider tokenCredentialProvider,
    IRegistryResolver registryResolver)
    : IRegistryCredentialsProvider
{
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly IRegistryResolver _registryResolver = registryResolver;

    /// <summary>
    /// Dynamically gets the RegistryCredentials for the specified registry in the following order of preference:
    ///     1. If we own the ACR, use the Azure SDK for authentication via the DefaultAzureCredential (no explicit credentials needed).
    ///     2. If we don't own the ACR, try to read the username/password passed in from the command line.
    ///     3. Return null if there are no credentials to be found.
    /// </summary>
    /// <param name="registry">The container registry to get credentials for.</param>
    /// <returns>Registry credentials</returns>
    public async ValueTask<RegistryCredentials?> GetCredentialsAsync(
        string registry,
        IRegistryCredentialsHost? credsHost)
    {
        RegistryInfo registryInfo = _registryResolver.Resolve(registry, credsHost);

        if (registryInfo.OwnedAcr is not null)
        {
            // If we're here, we know we own the ACR and have a service
            // connection we can use for authentication.
            return await GetAcrCredentialsWithOAuthAsync(registryInfo.OwnedAcr);
        }

        // Fall back to credentials explicitly passed in via command line.
        return registryInfo.ExplicitCredentials;
    }

    private async ValueTask<RegistryCredentials> GetAcrCredentialsWithOAuthAsync(RegistryConfiguration registryConfig)
    {
        if (!registryConfig.IsOwnedAcr(out var acr, out var serviceConnection))
        {
            throw new InvalidOperationException($"Registry '{registryConfig}' is not an owned ACR.");
        }

        TokenCredential tokenCredential = _tokenCredentialProvider.GetCredential(serviceConnection);
        var tenantGuid = Guid.Parse(serviceConnection.TenantId);
        string token = (await tokenCredential.GetTokenAsync(new TokenRequestContext([AzureScopes.Default]), CancellationToken.None)).Token;
        string refreshToken = await OAuthHelper.GetRefreshTokenAsync(_httpClientProvider.GetClient(), acr, tenantGuid, token);
        return new RegistryCredentials(Guid.Empty.ToString(), refreshToken);
    }
}
