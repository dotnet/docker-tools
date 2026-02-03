// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using Azure.Core;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class RegistryManifestClientFactoryTests
{
    private static readonly string s_tenant = Guid.Empty.ToString();

    [Fact]
    public void CreateAcrClient()
    {
        const string AcrName = "my-acr.azurecr.io";
        const string RepoName = "repo-name";
        IRegistryCredentialsHost credsHost = Mock.Of<IRegistryCredentialsHost>();

        IAcrContentClient contentClient = Mock.Of<IAcrContentClient>();

        Mock<IAcrContentClientFactory> acrContentClientFactoryMock = new();
        acrContentClientFactoryMock
            .Setup(o => o.Create(It.IsAny<Acr>(), RepoName))
            .Returns(contentClient);

        // Create a mock IRegistryResolver that returns authentication for the given registry
        Mock<IRegistryResolver> registryResolverMock = new();
        registryResolverMock
            .Setup(o => o.Resolve(AcrName, It.IsAny<IRegistryCredentialsHost?>()))
            .Returns(new RegistryInfo(AcrName, new RegistryAuthentication { ServiceConnection = new ServiceConnection() }, null));

        RegistryManifestClientFactory clientFactory = new(
            Mock.Of<IHttpClientProvider>(),
            acrContentClientFactoryMock.Object,
            registryResolverMock.Object);
        IRegistryManifestClient client = clientFactory.Create(AcrName, RepoName, credsHost);

        Assert.Same(contentClient, client);
    }

    [Theory]
    [InlineData("test.azurecr.io", "test.azurecr.io", "https://test.azurecr.io/")]
    [InlineData("mcr.microsoft.com", "mcr.microsoft.com", "https://mcr.microsoft.com/")]
    [InlineData(DockerHelper.DockerHubRegistry, DockerHelper.DockerHubApiRegistry, $"https://{DockerHelper.DockerHubApiRegistry}/")]
    public void CreateOtherRegistryClient(string registry, string effectiveRegistry, string expectedBaseUri)
    {
        // Create a mock IRegistryResolver that returns no authentication (external registry)
        Mock<IRegistryResolver> registryResolverMock = new();
        registryResolverMock
            .Setup(o => o.Resolve(registry, It.IsAny<IRegistryCredentialsHost?>()))
            .Returns(new RegistryInfo(effectiveRegistry, null, null));

        RegistryManifestClientFactory clientFactory = new(
            Mock.Of<IHttpClientProvider>(),
            Mock.Of<IAcrContentClientFactory>(),
            registryResolverMock.Object);
        IRegistryCredentialsHost credsHost = Mock.Of<IRegistryCredentialsHost>(host => host.Credentials == new Dictionary<string, RegistryCredentials>());
        IRegistryManifestClient client = clientFactory.Create(registry, "repo-name");

        Assert.IsType<RegistryApiClient>(client);

        RegistryApiClient registryApiClient = (RegistryApiClient)client;
        Assert.Equal(expectedBaseUri, registryApiClient.BaseUri.ToString());
    }
}
