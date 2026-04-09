// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Oras;
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
            Mock.Of<IAcrImageImporter>(),
            Mock.Of<IOrasService>(),
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

        var mockImporter = new Mock<IAcrImageImporter>();
        var mockOras = new Mock<IOrasService>();
        mockOras
            .Setup(o => o.GetReferrersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReferrerInfo>());

        var service = new CopyImageService(
            Mock.Of<ILogger<CopyImageService>>(),
            mockImporter.Object,
            mockOras.Object,
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

    /// <summary>
    /// When referrers exist for the source image, ImportImageAsync should import each referrer
    /// as an untagged artifact in addition to the main image.
    /// </summary>
    [Fact]
    public async Task ImportImageAsync_CopiesReferrersAlongWithSourceImage()
    {
        PublishConfiguration publishConfig = CreateAcrPublishConfig("myacr.azurecr.io");

        var mockImporter = new Mock<IAcrImageImporter>();
        var mockOras = new Mock<IOrasService>();
        mockOras
            .Setup(o => o.GetReferrersAsync("myacr.azurecr.io/repo:tag", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReferrerInfo>
            {
                new("myacr.azurecr.io/repo@sha256:ref1", "application/vnd.cncf.notary.signature"),
                new("myacr.azurecr.io/repo@sha256:ref2", "application/vnd.cncf.notary.signature")
            });

        var service = new CopyImageService(
            Mock.Of<ILogger<CopyImageService>>(),
            mockImporter.Object,
            mockOras.Object,
            ConfigurationHelper.CreateOptionsMock(publishConfig));

        await service.ImportImageAsync(
            destTagNames: ["mirror/repo:tag"],
            destAcrName: "myacr.azurecr.io",
            srcTagName: "repo:tag",
            srcRegistryName: "myacr.azurecr.io",
            isDryRun: false);

        // Main image import with TargetTags
        mockImporter.Verify(
            x => x.ImportImageAsync(
                "myacr.azurecr.io",
                It.IsAny<ResourceIdentifier>(),
                It.Is<ContainerRegistryImportImageContent>(c =>
                    c.TargetTags.Count == 1 && c.TargetTags[0] == "mirror/repo:tag")),
            Times.Once);

        // Two referrer imports with UntaggedTargetRepositories
        mockImporter.Verify(
            x => x.ImportImageAsync(
                "myacr.azurecr.io",
                It.IsAny<ResourceIdentifier>(),
                It.Is<ContainerRegistryImportImageContent>(c =>
                    c.UntaggedTargetRepositories.Count == 1
                    && c.UntaggedTargetRepositories[0] == "mirror/repo"
                    && c.Source.SourceImage == "repo@sha256:ref1")),
            Times.Once);

        mockImporter.Verify(
            x => x.ImportImageAsync(
                "myacr.azurecr.io",
                It.IsAny<ResourceIdentifier>(),
                It.Is<ContainerRegistryImportImageContent>(c =>
                    c.UntaggedTargetRepositories.Count == 1
                    && c.UntaggedTargetRepositories[0] == "mirror/repo"
                    && c.Source.SourceImage == "repo@sha256:ref2")),
            Times.Once);

        // Total: 1 main + 2 referrers = 3
        mockImporter.Verify(
            x => x.ImportImageAsync(
                It.IsAny<string>(),
                It.IsAny<ResourceIdentifier>(),
                It.IsAny<ContainerRegistryImportImageContent>()),
            Times.Exactly(3));
    }

    /// <summary>
    /// When no referrers exist, ImportImageAsync should import only the main image.
    /// </summary>
    [Fact]
    public async Task ImportImageAsync_NoReferrers_ImportsOnlySourceImage()
    {
        PublishConfiguration publishConfig = CreateAcrPublishConfig("myacr.azurecr.io");

        var mockImporter = new Mock<IAcrImageImporter>();
        var mockOras = new Mock<IOrasService>();
        mockOras
            .Setup(o => o.GetReferrersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReferrerInfo>());

        var service = new CopyImageService(
            Mock.Of<ILogger<CopyImageService>>(),
            mockImporter.Object,
            mockOras.Object,
            ConfigurationHelper.CreateOptionsMock(publishConfig));

        await service.ImportImageAsync(
            destTagNames: ["mirror/repo:tag"],
            destAcrName: "myacr.azurecr.io",
            srcTagName: "repo:tag",
            srcRegistryName: "myacr.azurecr.io",
            isDryRun: false);

        mockImporter.Verify(
            x => x.ImportImageAsync(
                It.IsAny<string>(),
                It.IsAny<ResourceIdentifier>(),
                It.IsAny<ContainerRegistryImportImageContent>()),
            Times.Once);
    }

    /// <summary>
    /// In dry-run mode, referrer discovery still runs (it is read-only) but the actual
    /// ACR import of referrers is skipped.
    /// </summary>
    [Fact]
    public async Task ImportImageAsync_DryRun_DiscoversReferrersButSkipsImport()
    {
        var mockOras = new Mock<IOrasService>();
        mockOras
            .Setup(o => o.GetReferrersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ReferrerInfo("myacr.azurecr.io/repo@sha256:abc123", "application/vnd.oci.image.manifest.v1+json")
            ]);

        var mockImporter = new Mock<IAcrImageImporter>();

        var service = new CopyImageService(
            Mock.Of<ILogger<CopyImageService>>(),
            mockImporter.Object,
            mockOras.Object,
            ConfigurationHelper.CreateOptionsMock(new PublishConfiguration()));

        await service.ImportImageAsync(
            destTagNames: ["myacr.azurecr.io/repo:tag"],
            destAcrName: "myacr.azurecr.io",
            srcTagName: "repo:tag",
            srcRegistryName: "myacr.azurecr.io",
            isDryRun: true);

        mockOras.Verify(
            o => o.GetReferrersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        mockImporter.Verify(
            o => o.ImportImageAsync(
                It.IsAny<string>(),
                It.IsAny<ResourceIdentifier>(),
                It.IsAny<ContainerRegistryImportImageContent>()),
            Times.Never);
    }

    private static PublishConfiguration CreateAcrPublishConfig(string acrServer)
    {
        return new PublishConfiguration
        {
            RegistryAuthentication =
            [
                new RegistryAuthentication
                {
                    Server = acrServer,
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
    }
}
