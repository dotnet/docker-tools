// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
[Export(typeof(IRegistryCredentialsProvider))]
[method: ImportingConstructor]
public class RegistryCredentialsProvider(ILoggerService loggerService, IHttpClientProvider httpClientProvider, IAzureTokenCredentialProvider tokenCredentialProvider) : IRegistryCredentialsProvider
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
        string registry, string? ownedAcr, IRegistryCredentialsHost? credsHost)
    {
        // Docker Hub's registry has a separate host name for its API
        string apiRegistry = registry == DockerHelper.DockerHubRegistry ?
            DockerHelper.DockerHubApiRegistry :
            registry;

        if (!string.IsNullOrEmpty(ownedAcr))
        {
            ownedAcr = DockerHelper.FormatAcrName(ownedAcr);
        }

        if (apiRegistry == ownedAcr)
        {
            return await GetAcrCredentialsWithOAuthAsync(_loggerService, apiRegistry);
        }

        return credsHost?.TryGetCredentials(apiRegistry) ?? null;
    }

    private async ValueTask<RegistryCredentials> GetAcrCredentialsWithOAuthAsync(ILoggerService logger, string apiRegistry)
    {
        TokenCredential tokenCredential = _tokenCredentialProvider.GetCredential();
        Guid tenantId = AuthHelper.GetTenantId(logger, tokenCredential);
        string token = (await tokenCredential.GetTokenAsync(new TokenRequestContext([AuthHelper.DefaultAzureManagementScope]), CancellationToken.None)).Token;
        string refreshToken = await OAuthHelper.GetRefreshTokenAsync(_httpClientProvider.GetClient(), apiRegistry, tenantId, token);
        return new RegistryCredentials(Guid.Empty.ToString(), refreshToken);
    }
}
