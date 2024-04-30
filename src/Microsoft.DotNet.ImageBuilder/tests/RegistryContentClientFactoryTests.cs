// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class RegistryContentClientFactoryTests
{
    [Theory]
    [InlineData("my-acr")]
    [InlineData("my-acr.azurecr.io")]
    public void CreateAcrClient(string ownedAcr)
    {
        RegistryContentClientFactory clientFactory = new(Mock.Of<IHttpClientProvider>());
        RegistryAuthContext registryAuthContext = new(ownedAcr, new Dictionary<string, RegistryCredentials>());
        IRegistryContentClient client = clientFactory.Create("my-acr.azurecr.io", "repo-name", registryAuthContext);

        Assert.IsType<ContainerRegistryContentClientWrapper>(client);
    }

    [Theory]
    [InlineData("test.azurecr.io", "https://test.azurecr.io/")]
    [InlineData("mcr.microsoft.com", "https://mcr.microsoft.com/")]
    [InlineData(DockerHelper.DockerHubRegistry, $"https://{DockerHelper.DockerHubApiRegistry}/")]
    public void CreateOtherRegistryClient(string registry, string expectedBaseUri)
    {
        RegistryContentClientFactory clientFactory = new(Mock.Of<IHttpClientProvider>());
        RegistryAuthContext registryAuthContext = new("my-acr", new Dictionary<string, RegistryCredentials>());
        IRegistryContentClient client = clientFactory.Create(registry, "repo-name", registryAuthContext);

        Assert.IsType<RegistryServiceClient>(client);

        RegistryServiceClient registryServiceClient = (RegistryServiceClient)client;
        Assert.Equal(expectedBaseUri, registryServiceClient.BaseUri.ToString());
    }
}
