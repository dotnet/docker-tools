// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using Azure.Containers.ContainerRegistry;
using Azure.Identity;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
[Export(typeof(IRegistryContentClientFactory))]
[method: ImportingConstructor]
internal class RegistryClientFactory(IHttpClientProvider httpClientProvider) : IRegistryContentClientFactory
{
    private readonly RegistryHttpClient _httpClient = httpClientProvider.GetRegistryClient();

    public IRegistryContentClient Create(string registry, string repo, IRegistryCredentialsHost credsHost)
    {
        // Docker Hub's registry has a separate host name for its API
        string apiRegistry = registry == DockerHelper.DockerHubRegistry ?
            DockerHelper.DockerHubApiRegistry :
            registry!;

        string ownedAcr = "dotnetdocker";

        if (!ownedAcr.EndsWith(DockerHelper.AcrDomain))
        {
            ownedAcr = $"{ownedAcr}.{DockerHelper.AcrDomain}";
        }
        
        if (apiRegistry == ownedAcr)
        {
            // If the target registry is the owned ACR, connect to it with the Azure library API. This handles all the Azure auth.
            return new ContainerRegistryContentClientWrapper(
                new ContainerRegistryContentClient(new Uri($"https://{apiRegistry}"), repo, new DefaultAzureCredential()));
        }
        else
        {
            BasicAuthenticationCredentials? basicAuthCreds = null;

            // Lookup the credentials, if any, for the registry where the image is located
            if (credsHost.Credentials.TryGetValue(registry, out RegistryCredentials? registryCreds))
            {
                basicAuthCreds = new BasicAuthenticationCredentials(registryCreds.Username, registryCreds.Password);
            }

            return new RegistryServiceClient(apiRegistry, repo, _httpClient, basicAuthCreds);
        }
    }
}
#nullable disable
