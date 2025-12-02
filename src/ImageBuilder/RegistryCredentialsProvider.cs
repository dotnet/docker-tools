// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public class RegistryCredentialsProvider(
    ILoggerService loggerService,
    IHttpClientProvider httpClientProvider,
    IAzureTokenCredentialProvider tokenCredentialProvider,
    IOptions<PublishConfiguration> publishConfigOptions)
    : IRegistryCredentialsProvider
{
    private readonly ILoggerService _loggerService = loggerService;
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly PublishConfiguration _publishConfig = publishConfigOptions.Value;

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
        var explicitCreds = credsHost?.TryGetCredentials(registry);

        // Docker Hub's registry has a separate host name for its API
        if (registry == DockerHelper.DockerHubRegistry)
        {
            registry = DockerHelper.DockerHubApiRegistry;

            // This is definitely not an ACR, so don't bother checking ACRs
            // passed in via the publish configuration.
            return explicitCreds;
        }

        // Create an Acr reference to compare against

        // Compare against all the ACRs passed in via the publish configuration
        var maybeOwnedAcr = new Acr(registry);
        var knownAcrs = _publishConfig.GetKnownAcrConfigurations();
        var ownedAcr = knownAcrs.FirstOrDefault(acr => acr.Registry == maybeOwnedAcr);

        if (ownedAcr is not null && ownedAcr.ServiceConnection is not null)
        {
            // If we're here, we know we own the ACR and have a service
            // connection we can use for authentication.
            return await GetAcrCredentialsWithOAuthAsync(ownedAcr);
        }

        // Fall back to credentials explicitly passed in via command line.
        return explicitCreds;
    }

    private async ValueTask<RegistryCredentials> GetAcrCredentialsWithOAuthAsync(AcrConfiguration acrConfig)
    {
        if (acrConfig.ServiceConnection is null)
        {
            throw new ArgumentNullException(nameof(acrConfig.ServiceConnection));
        }

        if (acrConfig.Registry is null)
        {
            throw new ArgumentNullException(nameof(acrConfig.Registry));
        }

        TokenCredential tokenCredential = _tokenCredentialProvider.GetCredential(acrConfig.ServiceConnection);
        var tenantGuid = Guid.Parse(acrConfig.ServiceConnection.TenantId);
        string token = (await tokenCredential.GetTokenAsync(new TokenRequestContext([AzureScopes.DefaultAzureManagementScope]), CancellationToken.None)).Token;
        string refreshToken = await OAuthHelper.GetRefreshTokenAsync(_httpClientProvider.GetClient(), acrConfig.Registry.Name, tenantGuid, token);
        return new RegistryCredentials(Guid.Empty.ToString(), refreshToken);
    }
}
