// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Options;
using Moq;
using Microsoft.Extensions.Logging;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class ImageSigningServiceTests
{
    private const string ArtifactStagingDir = "/artifacts/staging";

    [Fact]
    public async Task SignImagesAsync_EmptyInput_ReturnsEmpty()
    {
        var service = CreateService();

        var imageArtifactDetails = new ImageArtifactDetails { Repos = [] };

        var results = await service.SignImagesAsync(imageArtifactDetails, signingKeyCode: 100);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SignImagesAsync_SkipsImagesWithoutManifestOrDigest()
    {
        var mockOras = new Mock<IOrasService>();
        var service = CreateService(mockOras: mockOras);

        var imageArtifactDetails = new ImageArtifactDetails
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "myregistry.azurecr.io/dotnet/runtime",
                    Images =
                    [
                        new ImageData
                        {
                            Manifest = null,
                            Platforms = []
                        }
                    ]
                }
            ]
        };

        var results = await service.SignImagesAsync(imageArtifactDetails, signingKeyCode: 100);

        results.ShouldBeEmpty();
        mockOras.Verify(
            s => s.GetDescriptorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SignImagesAsync_SkipsPlatformsWithoutDigest()
    {
        var mockOras = CreateMockOrasService();
        var fileSystem = new InMemoryFileSystem();
        var mockEsrp = CreateEsrpMockWithCoseOverwrite(fileSystem);

        var service = CreateService(mockOras, mockEsrp: mockEsrp, fileSystem: fileSystem);

        var imageArtifactDetails = new ImageArtifactDetails
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "myregistry.azurecr.io/dotnet/runtime",
                    Images =
                    [
                        new ImageData
                        {
                            Platforms =
                            [
                                new PlatformData { Digest = "sha256:abc123", SimpleTags = ["8.0"] },
                                new PlatformData { Digest = "", SimpleTags = ["no-digest"] },
                                new PlatformData { Digest = null!, SimpleTags = ["null-digest"] }
                            ]
                        }
                    ]
                }
            ]
        };

        var results = await service.SignImagesAsync(imageArtifactDetails, signingKeyCode: 100);

        results.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SignImagesAsync_OrchestratesFullPipeline()
    {
        var mockOras = CreateMockOrasService();
        var fileSystem = new InMemoryFileSystem();
        var mockEsrp = CreateEsrpMockWithCoseOverwrite(fileSystem);

        mockOras
            .Setup(s => s.PushSignatureAsync(
                It.IsAny<OrasDescriptor>(), It.IsAny<PayloadSigningResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sha256:sigdigest");

        var service = CreateService(mockOras, mockEsrp: mockEsrp, fileSystem: fileSystem);

        var imageArtifactDetails = new ImageArtifactDetails
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "myregistry.azurecr.io/dotnet/runtime",
                    Images =
                    [
                        new ImageData
                        {
                            Platforms =
                            [
                                new PlatformData { Digest = "sha256:abc123" }
                            ]
                        }
                    ]
                }
            ]
        };

        var results = await service.SignImagesAsync(imageArtifactDetails, signingKeyCode: 42);

        mockOras.Verify(
            s => s.GetDescriptorAsync("sha256:abc123", It.IsAny<CancellationToken>()),
            Times.Once);
        mockEsrp.Verify(
            e => e.SignFilesAsync(It.IsAny<IEnumerable<string>>(), 42, It.IsAny<CancellationToken>()),
            Times.Once);
        mockOras.Verify(
            s => s.PushSignatureAsync(
                It.IsAny<OrasDescriptor>(), It.IsAny<PayloadSigningResult>(), It.IsAny<CancellationToken>()),
            Times.Once);

        results.Count.ShouldBe(1);
        results[0].SignatureDigest.ShouldBe("sha256:sigdigest");
    }

    [Fact]
    public async Task SignImagesAsync_ResolvesPlatformAndManifestListDigests()
    {
        var mockOras = CreateMockOrasService();
        var fileSystem = new InMemoryFileSystem();
        var mockEsrp = CreateEsrpMockWithCoseOverwrite(fileSystem);

        var service = CreateService(mockOras, mockEsrp: mockEsrp, fileSystem: fileSystem);

        var imageArtifactDetails = new ImageArtifactDetails
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "myregistry.azurecr.io/dotnet/runtime",
                    Images =
                    [
                        new ImageData
                        {
                            Manifest = new ManifestData
                            {
                                Digest = "sha256:manifest123",
                                SharedTags = ["8.0", "latest"]
                            },
                            Platforms =
                            [
                                new PlatformData { Digest = "sha256:abc123", SimpleTags = ["8.0-alpine"] },
                                new PlatformData { Digest = "sha256:def456", SimpleTags = ["8.0-jammy"] }
                            ]
                        }
                    ]
                }
            ]
        };

        var results = await service.SignImagesAsync(imageArtifactDetails, signingKeyCode: 100);

        results.Count.ShouldBe(3);
        results.Select(r => r.ImageName).ShouldBe(
            ["sha256:abc123", "sha256:def456", "sha256:manifest123"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task SignImagesAsync_WritesCorrectPayloadToDisk()
    {
        var mockOras = new Mock<IOrasService>();
        mockOras
            .Setup(s => s.GetDescriptorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string reference, CancellationToken _) => new OrasDescriptor
            {
                MediaType = "application/vnd.oci.image.index.v1+json",
                Digest = reference,
                Size = 5678
            });

        var fileSystem = new InMemoryFileSystem();
        var mockEsrp = CreateEsrpMockWithCoseOverwrite(fileSystem);

        var service = CreateService(mockOras, mockEsrp: mockEsrp, fileSystem: fileSystem);

        var imageArtifactDetails = new ImageArtifactDetails
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "myregistry.azurecr.io/dotnet/runtime",
                    Images =
                    [
                        new ImageData
                        {
                            Manifest = new ManifestData
                            {
                                Digest = "sha256:manifest123",
                                SharedTags = ["8.0", "latest"]
                            }
                        }
                    ]
                }
            ]
        };

        await service.SignImagesAsync(imageArtifactDetails, signingKeyCode: 100);

        fileSystem.FilesWritten.ShouldContain(
            path => path.Contains("sha256-manifest123") && path.EndsWith(".payload"));
    }

    [Fact]
    public async Task SignImagesAsync_MissingArtifactStagingDir_ThrowsInvalidOperation()
    {
        var mockOras = CreateMockOrasService();
        var buildConfig = new BuildConfiguration { ArtifactStagingDirectory = "" };
        var service = CreateService(mockOras, buildConfig: buildConfig);

        var imageArtifactDetails = new ImageArtifactDetails
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "registry.io/repo",
                    Images =
                    [
                        new ImageData
                        {
                            Platforms =
                            [
                                new PlatformData { Digest = "sha256:abc123" }
                            ]
                        }
                    ]
                }
            ]
        };

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => service.SignImagesAsync(imageArtifactDetails, signingKeyCode: 100));

        ex.Message.ShouldContain("ArtifactStagingDirectory");
    }

    private static Mock<IOrasService> CreateMockOrasService()
    {
        var mock = new Mock<IOrasService>();
        mock
            .Setup(s => s.GetDescriptorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string reference, CancellationToken _) => new OrasDescriptor
            {
                MediaType = "application/vnd.oci.image.manifest.v1+json",
                Digest = reference,
                Size = 1234
            });
        mock
            .Setup(s => s.PushSignatureAsync(
                It.IsAny<OrasDescriptor>(), It.IsAny<PayloadSigningResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrasDescriptor _, PayloadSigningResult p, CancellationToken _) =>
                $"sha256:sig-{p.ImageName}");
        return mock;
    }

    /// <summary>
    /// Creates an ESRP mock that overwrites payload files with minimal COSE_Sign1 bytes,
    /// simulating what ESRP does in production.
    /// </summary>
    private static Mock<IEsrpSigningService> CreateEsrpMockWithCoseOverwrite(InMemoryFileSystem fileSystem)
    {
        var mock = new Mock<IEsrpSigningService>();
        mock
            .Setup(e => e.SignFilesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, int, CancellationToken>((files, _, _) =>
            {
                foreach (var file in files)
                {
                    fileSystem.AddFile(file, CreateMinimalCoseSign1Bytes());
                }
            })
            .Returns(Task.CompletedTask);
        return mock;
    }

    /// <summary>
    /// Creates minimal valid COSE_Sign1 bytes with a single certificate in the x5chain.
    /// </summary>
    private static byte[] CreateMinimalCoseSign1Bytes()
    {
        var writer = new CborWriter();
        writer.WriteTag((CborTag)18); // COSE_Sign1

        writer.WriteStartArray(4);

        // Protected header (empty byte string)
        writer.WriteByteString([]);

        // Unprotected header with x5chain (key 33)
        writer.WriteStartMap(1);
        writer.WriteInt32(33);
        writer.WriteByteString([0x01, 0x02, 0x03]); // fake cert bytes
        writer.WriteEndMap();

        // Payload
        writer.WriteByteString([]);

        // Signature
        writer.WriteByteString([]);

        writer.WriteEndArray();
        return writer.Encode();
    }

    private static ImageSigningService CreateService(
        Mock<IOrasService>? mockOras = null,
        Mock<IEsrpSigningService>? mockEsrp = null,
        InMemoryFileSystem? fileSystem = null,
        BuildConfiguration? buildConfig = null)
    {
        buildConfig ??= new BuildConfiguration { ArtifactStagingDirectory = ArtifactStagingDir };
        fileSystem ??= new InMemoryFileSystem();

        return new ImageSigningService(
            (mockOras ?? new Mock<IOrasService>()).Object,
            (mockEsrp ?? CreateEsrpMockWithCoseOverwrite(fileSystem)).Object,
            Mock.Of<ILogger<ImageSigningService>>(),
            fileSystem,
            Options.Create(buildConfig));
    }
}
