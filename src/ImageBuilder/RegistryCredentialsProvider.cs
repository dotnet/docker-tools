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
    ///     1. If we have authentication configured (e.g., service connection for ACR), use Azure SDK for authentication.
    ///     2. If we don't have authentication configured, try to read the username/password passed in from the command line.
    ///     3. Return null if there are no credentials to be found.
    /// </summary>
    /// <param name="registry">The container registry to get credentials for.</param>
    /// <returns>Registry credentials</returns>
    public async ValueTask<RegistryCredentials?> GetCredentialsAsync(
        string registry,
        IRegistryCredentialsHost? credsHost)
    {
        RegistryInfo registryInfo = _registryResolver.Resolve(registry, credsHost);

        if (registryInfo.RegistryAuthentication?.ServiceConnection is not null)
        {
            // If we're here, we have authentication configured with a service
            // connection we can use for authentication.
            return await GetAcrCredentialsWithOAuthAsync(registryInfo.EffectiveRegistry, registryInfo.RegistryAuthentication);
        }

        // Fall back to credentials explicitly passed in via command line.
        return registryInfo.ExplicitCredentials;
    }

    private async ValueTask<RegistryCredentials> GetAcrCredentialsWithOAuthAsync(string registry, RegistryAuthentication auth)
    {
        if (auth.ServiceConnection is null)
        {
            throw new InvalidOperationException($"Registry '{registry}' does not have a service connection configured.");
        }

        var acr = Acr.Parse(registry);
        TokenCredential tokenCredential = _tokenCredentialProvider.GetCredential(auth.ServiceConnection);
        var tenantGuid = Guid.Parse(auth.ServiceConnection.TenantId);
        string token = (await tokenCredential.GetTokenAsync(new TokenRequestContext([AzureScopes.Default]), CancellationToken.None)).Token;
        string refreshToken = await OAuthHelper.GetRefreshTokenAsync(_httpClientProvider.GetClient(), acr, tenantGuid, token);
        return new RegistryCredentials(Guid.Empty.ToString(), refreshToken);
    }
}
