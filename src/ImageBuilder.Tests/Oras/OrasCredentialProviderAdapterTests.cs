// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Oras;
using Moq;
using OrasProject.Oras.Registry.Remote.Auth;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Oras;

public class OrasCredentialProviderAdapterTests
{
    [Fact]
    public async Task ResolveCredentialAsync_ReturnsCredentials_WhenProviderReturnsCredentials()
    {
        var mockProvider = new Mock<IRegistryCredentialsProvider>();
        mockProvider
            .Setup(p => p.GetCredentialsAsync("registry.io", null))
            .ReturnsAsync(new RegistryCredentials("testuser", "testpass"));

        var adapter = new OrasCredentialProviderAdapter(mockProvider.Object);

        Credential result = await adapter.ResolveCredentialAsync("registry.io", CancellationToken.None);

        result.Username.ShouldBe("testuser");
        result.Password.ShouldBe("testpass");
        result.RefreshToken.ShouldBe(string.Empty);
        result.AccessToken.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ResolveCredentialAsync_ReturnsDefault_WhenProviderReturnsNull()
    {
        var mockProvider = new Mock<IRegistryCredentialsProvider>();
        mockProvider
            .Setup(p => p.GetCredentialsAsync("registry.io", null))
            .ReturnsAsync((RegistryCredentials?)null);

        var adapter = new OrasCredentialProviderAdapter(mockProvider.Object);

        Credential result = await adapter.ResolveCredentialAsync("registry.io", CancellationToken.None);

        result.ShouldBe(default(Credential));
    }

    [Fact]
    public async Task ResolveCredentialAsync_PassesCredentialsHost_WhenProvided()
    {
        var mockProvider = new Mock<IRegistryCredentialsProvider>();
        var mockHost = new Mock<IRegistryCredentialsHost>();

        mockProvider
            .Setup(p => p.GetCredentialsAsync("registry.io", mockHost.Object))
            .ReturnsAsync(new RegistryCredentials("user", "pass"));

        var adapter = new OrasCredentialProviderAdapter(mockProvider.Object, mockHost.Object);

        await adapter.ResolveCredentialAsync("registry.io", CancellationToken.None);

        mockProvider.Verify(p => p.GetCredentialsAsync("registry.io", mockHost.Object), Times.Once);
    }
}
