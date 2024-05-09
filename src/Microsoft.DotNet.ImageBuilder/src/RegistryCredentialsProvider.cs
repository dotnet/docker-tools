// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
[Export(typeof(IRegistryCredentialsProvider))]
[method: ImportingConstructor]
public class RegistryCredentialsProvider(IHttpClientProvider httpClientProvider) : IRegistryCredentialsProvider
{
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;

    /// <summary>
    /// Dynamically gets the RegistryCredentials for the specified registry in the following order of preference:
    ///     1. If we own the registry, use OAuth to get the credentials.
    ///     2. Read the credentials passed in from the command line.
    ///     3. Return null if there are no credentials to be found.
    /// </summary>
    /// <param name="registry">The container registry to get credentials for.</param>
    /// <returns>Registry credentials</returns>
    public async ValueTask<RegistryCredentials?> GetCredentialsAsync(
        string registry, string? ownedAcr, IRegistryCredentialsHost? credsHost)
    {
        string? tenant = credsHost?.Tenant;

        // Docker Hub's registry has a separate host name for its API
        string apiRegistry = registry == DockerHelper.DockerHubRegistry ?
            DockerHelper.DockerHubApiRegistry :
            registry;

        if (ownedAcr is not null)
        {
            ownedAcr = DockerHelper.FormatAcrName(ownedAcr);
        }

        if (apiRegistry == ownedAcr && tenant is not null)
        {
            return await GetAcrCredentialsWithOAuthAsync(apiRegistry, tenant);
        }

        return credsHost?.TryGetCredentials(apiRegistry) ?? null;
    }

    private async ValueTask<RegistryCredentials> GetAcrCredentialsWithOAuthAsync(string apiRegistry, string tenant)
    {
        string eidToken = await AuthHelper.GetDefaultAccessTokenAsync();
        string refreshToken = await OAuthHelper.GetRefreshTokenAsync(_httpClientProvider.GetClient(), apiRegistry, tenant, eidToken);
        return new RegistryCredentials(Guid.Empty.ToString(), refreshToken);
    }
}
