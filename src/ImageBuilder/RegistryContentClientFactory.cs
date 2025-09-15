// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

public class RegistryContentClientFactory(
    IHttpClientProvider httpClientProvider,
    IContainerRegistryContentClientFactory containerRegistryContentClientFactory)
    : IRegistryContentClientFactory
{
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;
    private readonly IContainerRegistryContentClientFactory _containerRegistryContentClientFactory =
        containerRegistryContentClientFactory;

    public IRegistryContentClient Create(
        string registry,
        string repo,
        string? ownedAcr = null,
        IServiceConnection? serviceConnection = null,
        IRegistryCredentialsHost? credsHost = null)
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
            // If the target registry is the owned ACR, connect to it with the Azure library API. This handles all the Azure auth.
            return _containerRegistryContentClientFactory.Create(ownedAcr, repo, serviceConnection);
        }

        // Look up the credentials, if any, for the registry where the image is located
        RegistryCredentials? registryCreds = credsHost?.TryGetCredentials(registry);
        return new RegistryServiceClient(apiRegistry, repo, _httpClientProvider.GetRegistryClient(), registryCreds);
    }
}
