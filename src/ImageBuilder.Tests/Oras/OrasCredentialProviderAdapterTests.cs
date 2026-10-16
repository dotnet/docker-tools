// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Oras;
using Moq;
using OrasProject.Oras.Registry.Remote.Auth;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests.Oras;

[TestClass]
public class OrasCredentialProviderAdapterTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    public async Task ResolveCredentialAsync_ReturnsCredentials_WhenProviderReturnsCredentials()
    {
        Mock<IRegistryCredentialsProvider> mockProvider = new Mock<IRegistryCredentialsProvider>();
        mockProvider
            .Setup(p => p.GetCredentialsAsync("registry.io", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryCredentials("testuser", "testpass"));

        OrasCredentialProviderAdapter adapter = new OrasCredentialProviderAdapter(mockProvider.Object);

        Credential result = await adapter.ResolveCredentialAsync("registry.io", CancellationToken.None);

        result.Username.ShouldBe("testuser");
        result.Password.ShouldBe("testpass");
        result.RefreshToken.ShouldBe(string.Empty);
        result.AccessToken.ShouldBe(string.Empty);
    }

    [TestMethod]
    public async Task ResolveCredentialAsync_ReturnsDefault_WhenProviderReturnsNull()
    {
        Mock<IRegistryCredentialsProvider> mockProvider = new Mock<IRegistryCredentialsProvider>();
        mockProvider
            .Setup(p => p.GetCredentialsAsync("registry.io", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegistryCredentials?)null);

        OrasCredentialProviderAdapter adapter = new OrasCredentialProviderAdapter(mockProvider.Object);

        Credential result = await adapter.ResolveCredentialAsync("registry.io", CancellationToken.None);

        result.ShouldBe(default(Credential));
    }

    [TestMethod]
    public async Task ResolveCredentialAsync_PassesCredentialsHost_WhenProvided()
    {
        Mock<IRegistryCredentialsProvider> mockProvider = new Mock<IRegistryCredentialsProvider>();
        Mock<IRegistryCredentialsHost> mockHost = new Mock<IRegistryCredentialsHost>();

        mockProvider
            .Setup(p => p.GetCredentialsAsync("registry.io", mockHost.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryCredentials("user", "pass"));

        OrasCredentialProviderAdapter adapter = new OrasCredentialProviderAdapter(mockProvider.Object, mockHost.Object);

        await adapter.ResolveCredentialAsync("registry.io", CancellationToken.None);

        mockProvider.Verify(p => p.GetCredentialsAsync("registry.io", mockHost.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow("registry-1.docker.io")]
    [DataRow("REGISTRY-1.DOCKER.IO")]
    public async Task ResolveCredentialAsync_NormalizesDockerHubApiHostToDockerIo(string apiHost)
    {
        Mock<IRegistryCredentialsProvider> mockProvider = new Mock<IRegistryCredentialsProvider>();
        mockProvider
            .Setup(p => p.GetCredentialsAsync("docker.io", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryCredentials("hubuser", "hubpass"));

        OrasCredentialProviderAdapter adapter = new OrasCredentialProviderAdapter(mockProvider.Object);

        Credential result = await adapter.ResolveCredentialAsync(apiHost, CancellationToken.None);

        result.Username.ShouldBe("hubuser");
        result.Password.ShouldBe("hubpass");
        mockProvider.Verify(p => p.GetCredentialsAsync("docker.io", null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
