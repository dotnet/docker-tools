// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.Composition;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
internal class RegistryClientFactory : IRegistryClientFactory
{
    private readonly RegistryHttpClient _httpClient;

    [ImportingConstructor]
    public RegistryClientFactory(IHttpClientProvider httpClientProvider)
    {
        _httpClient = httpClientProvider.GetRegistryClient();
    }

    public IRegistryClient Create(string registry, IRegistryCredentialsHost credsHost)
    {
        BasicAuthenticationCredentials? basicAuthCreds = null;

        // Lookup the credentials, if any, for the registry where the image is located
        if (credsHost.Credentials.TryGetValue(registry, out RegistryCredentials? registryCreds))
        {
            basicAuthCreds = new BasicAuthenticationCredentials(registryCreds.Username, registryCreds.Password);
        }

        // Docker Hub's registry has a separate host name for its API
        string apiRegistry = registry == DockerHelper.DockerHubRegistry ?
            DockerHelper.DockerHubApiRegistry :
            registry!;

        return new RegistryServiceClient(apiRegistry, _httpClient, basicAuthCreds);
    }
}
#nullable disable
