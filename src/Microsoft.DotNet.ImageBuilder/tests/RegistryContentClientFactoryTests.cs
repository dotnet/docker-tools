// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Azure.Core;
using Microsoft.DotNet.ImageBuilder.Commands;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class RegistryContentClientFactoryTests
{
    private static readonly string s_tenant = Guid.Empty.ToString();

    [Theory]
    [InlineData("my-acr")]
    [InlineData("my-acr.azurecr.io")]
    public void CreateAcrClient(string ownedAcr)
    {
        const string AcrName = "my-acr.azurecr.io";
        const string RepoName = "repo-name";
        IRegistryCredentialsHost credsHost = Mock.Of<IRegistryCredentialsHost>();
        IServiceConnection serviceConnection = Mock.Of<IServiceConnection>();

        IContainerRegistryContentClient contentClient = Mock.Of<IContainerRegistryContentClient>();

        Mock<IContainerRegistryContentClientFactory> acrContentClientFactoryMock = new();
        acrContentClientFactoryMock
            .Setup(o => o.Create(AcrName, RepoName, serviceConnection))
            .Returns(contentClient);


        RegistryContentClientFactory clientFactory = new(
            Mock.Of<IHttpClientProvider>(),
            acrContentClientFactoryMock.Object);
        IRegistryContentClient client = clientFactory.Create(AcrName, RepoName, ownedAcr, serviceConnection, credsHost);

        Assert.Same(contentClient, client);
    }

    [Theory]
    [InlineData("test.azurecr.io", "https://test.azurecr.io/")]
    [InlineData("mcr.microsoft.com", "https://mcr.microsoft.com/")]
    [InlineData(DockerHelper.DockerHubRegistry, $"https://{DockerHelper.DockerHubApiRegistry}/")]
    public void CreateOtherRegistryClient(string registry, string expectedBaseUri)
    {
        ManifestOptions options = Mock.Of<ManifestOptions>(options => options.RegistryOverride == "my-acr");
        RegistryContentClientFactory clientFactory = new(
            Mock.Of<IHttpClientProvider>(),
            Mock.Of<IContainerRegistryContentClientFactory>());
        IRegistryCredentialsHost credsHost = Mock.Of<IRegistryCredentialsHost>(host => host.Credentials == new Dictionary<string, RegistryCredentials>());
        IRegistryContentClient client = clientFactory.Create(registry, "repo-name");

        Assert.IsType<RegistryServiceClient>(client);

        RegistryServiceClient registryServiceClient = (RegistryServiceClient)client;
        Assert.Equal(expectedBaseUri, registryServiceClient.BaseUri.ToString());
    }
}
