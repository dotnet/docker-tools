// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

public class RegistryManifestClientFactory(
    IHttpClientProvider httpClientProvider,
    IAcrContentClientFactory acrContentClientFactory,
    IRegistryResolver registryResolver)
    : IRegistryManifestClientFactory
{
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;
    private readonly IAcrContentClientFactory _acrContentClientFactory = acrContentClientFactory;
    private readonly IRegistryResolver _registryResolver = registryResolver;

    public IRegistryManifestClient Create(string registry, string repo, IRegistryCredentialsHost? credsHost = null)
    {
        RegistryInfo registryInfo = _registryResolver.Resolve(registry, credsHost);

        if (registryInfo.RegistryAuthentication?.ServiceConnection is not null)
        {
            // If we're here, we have authentication configured with a service
            // connection we can use for authentication.
            // Create using Azure SDK.
            var acr = Acr.Parse(registryInfo.EffectiveRegistry);
            return _acrContentClientFactory.Create(acr, repo);
        }

        // Fall back to credentials explicitly passed in via command line.
        // Create using explicit credentials, if any.
        return new RegistryApiClient(
            registryInfo.EffectiveRegistry,
            repo,
            _httpClientProvider.GetRegistryClient(),
            registryInfo.ExplicitCredentials);
    }
}
