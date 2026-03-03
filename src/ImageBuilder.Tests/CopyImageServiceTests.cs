// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class CopyImageServiceTests
{
    /// <summary>
    /// When isDryRun is true and the publish configuration has no registry authentication,
    /// ImportImageAsync should succeed without throwing. This scenario occurs in PR validation
    /// pipelines where appsettings.json is not generated.
    /// </summary>
    [Fact]
    public async Task ImportImageAsync_DryRun_DoesNotRequirePublishConfiguration()
    {
        var emptyConfig = new PublishConfiguration();
        var service = new CopyImageService(
            Mock.Of<ILogger<CopyImageService>>(),
            Mock.Of<IAcrRegistryImporter>(),
            ConfigurationHelper.CreateOptionsMock(emptyConfig));

        await Should.NotThrowAsync(() =>
            service.ImportImageAsync(
                destTagNames: ["myacr.azurecr.io/repo:tag"],
                destAcrName: "myacr.azurecr.io",
                srcTagName: "repo:tag",
                srcRegistryName: "docker.io",
                isDryRun: true));
    }

    /// <summary>
    /// When the source registry is external (e.g., docker.io) and not in the publish configuration,
    /// ImportImageAsync should proceed past the registry lookup without throwing. External registries
    /// use RegistryAddress + Credentials for ACR import, not ResourceId.
    /// </summary>
    [Fact]
    public async Task ImportImageAsync_ExternalSourceRegistry_DoesNotRequireSourceRegistryInPublishConfig()
    {
        var publishConfig = new PublishConfiguration
        {
            RegistryAuthentication =
            [
                new RegistryAuthentication
                {
                    Server = "myacr.azurecr.io",
                    ResourceGroup = "my-rg",
                    Subscription = Guid.NewGuid().ToString(),
                    ServiceConnection = new ServiceConnection
                    {
                        Name = "test",
                        Id = Guid.NewGuid().ToString(),
                        TenantId = Guid.NewGuid().ToString(),
                        ClientId = Guid.NewGuid().ToString()
                    }
                }
            ]
        };

        var mockImporter = new Mock<IAcrRegistryImporter>();

        var service = new CopyImageService(
            Mock.Of<ILogger<CopyImageService>>(),
            mockImporter.Object,
            ConfigurationHelper.CreateOptionsMock(publishConfig));

        await service.ImportImageAsync(
            destTagNames: ["mirror/repo:tag"],
            destAcrName: "myacr.azurecr.io",
            srcTagName: "repo:tag",
            srcRegistryName: "docker.io",
            isDryRun: false);

        // Verify the importer was called, proving execution reached the import step
        // (past the external registry lookup that previously threw)
        mockImporter.Verify(
            x => x.ImportImageAsync(
                "myacr.azurecr.io",
                It.IsAny<ResourceIdentifier>(),
                It.Is<ContainerRegistryImportImageContent>(c =>
                    c.Source.RegistryAddress == "docker.io" && c.Source.ResourceId == null!)),
            Times.Once);
    }
}
