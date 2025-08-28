// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public class RegistryCredentialsProvider(
    ILoggerService loggerService,
    IHttpClientProvider httpClientProvider,
    IAzureTokenCredentialProvider tokenCredentialProvider)
    : IRegistryCredentialsProvider
{
    private readonly ILoggerService _loggerService = loggerService;
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;

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
        string? ownedAcr,
        IServiceConnection? serviceConnection,
        IRegistryCredentialsHost? credsHost)
    {
        // Docker Hub's registry has a separate host name for its API
        if (registry == DockerHelper.DockerHubRegistry)
        {
            registry = DockerHelper.DockerHubApiRegistry;
        }

        if (!string.IsNullOrEmpty(ownedAcr))
        {
            ownedAcr = DockerHelper.FormatAcrName(ownedAcr);
        }

        if (registry == ownedAcr && serviceConnection != null)
        {
            return await GetAcrCredentialsWithOAuthAsync(_loggerService, registry, serviceConnection);
        }

        return credsHost?.TryGetCredentials(registry) ?? null;
    }

    private async ValueTask<RegistryCredentials> GetAcrCredentialsWithOAuthAsync(
        ILoggerService logger,
        string apiRegistry,
        IServiceConnection serviceConnection)
    {
        TokenCredential tokenCredential = _tokenCredentialProvider.GetCredential(serviceConnection);
        var tenantGuid = Guid.Parse(serviceConnection.TenantId);
        string token = (await tokenCredential.GetTokenAsync(new TokenRequestContext([AzureScopes.DefaultAzureManagementScope]), CancellationToken.None)).Token;
        string refreshToken = await OAuthHelper.GetRefreshTokenAsync(_httpClientProvider.GetClient(), apiRegistry, tenantGuid, token);
        return new RegistryCredentials(Guid.Empty.ToString(), refreshToken);
    }
}
