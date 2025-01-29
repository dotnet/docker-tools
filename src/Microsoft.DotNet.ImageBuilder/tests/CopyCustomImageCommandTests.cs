// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Commands;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class CopyCustomImageCommandTests
{
    [Fact]
    public async Task DockerHubImage()
    {
        Mock<ICopyImageService> copyImageServiceMock = new();

        CopyCustomImageCommand cmd = new(copyImageServiceMock.Object);
        cmd.Options.ImageName = "anchore/syft:v1.19.0";
        cmd.Options.Subscription = "test-sub";
        cmd.Options.ResourceGroup = "test-rg";
        cmd.Options.DestinationRegistry = "my-registry.azurecr.io";
        cmd.Options.CredentialsOptions.Credentials.Add("docker.io", new RegistryCredentials("user", "pass"));

        await cmd.ExecuteAsync();

        copyImageServiceMock.Verify(
            x => x.ImportImageAsync(
                cmd.Options.Subscription,
                cmd.Options.ResourceGroup,
                new[] { cmd.Options.ImageName },
                cmd.Options.DestinationRegistry,
                cmd.Options.ImageName,
                "docker.io",
                null,
                It.Is<ContainerRegistryImportSourceCredentials>(
                    x => x.Username == "user" && x.Password == "pass"),
                It.IsAny<bool>())
                );
    }
}
