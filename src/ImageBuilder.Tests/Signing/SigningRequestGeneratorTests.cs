// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.DotNet.ImageBuilder.Signing;
using Moq;
using OrasProject.Oras.Oci;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class SigningRequestGeneratorTests
{
    [Fact]
    public async Task GeneratePlatformSigningRequestsAsync_ReturnsPlatformRequests()
    {
        var mockDescriptorService = new Mock<IOrasDescriptorService>();
        mockDescriptorService
            .Setup(s => s.GetDescriptorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string reference, CancellationToken _) => new Descriptor
            {
                MediaType = "application/vnd.oci.image.manifest.v1+json",
                Digest = reference.Split('@')[1],
                Size = 1234
            });

        var mockLogger = new Mock<ILoggerService>();

        var generator = new SigningRequestGenerator(mockDescriptorService.Object, mockLogger.Object);

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
                                new PlatformData { Digest = "sha256:abc123", SimpleTags = ["8.0-alpine"] },
                                new PlatformData { Digest = "sha256:def456", SimpleTags = ["8.0-jammy"] }
                            ]
                        }
                    ]
                }
            ]
        };

        var requests = await generator.GeneratePlatformSigningRequestsAsync(imageArtifactDetails);

        requests.Count.ShouldBe(2);
        requests[0].ImageName.ShouldBe("myregistry.azurecr.io/dotnet/runtime@sha256:abc123");
        requests[1].ImageName.ShouldBe("myregistry.azurecr.io/dotnet/runtime@sha256:def456");
    }

    [Fact]
    public async Task GeneratePlatformSigningRequestsAsync_SkipsPlatformsWithoutDigest()
    {
        var mockDescriptorService = new Mock<IOrasDescriptorService>();
        mockDescriptorService
            .Setup(s => s.GetDescriptorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Descriptor
            {
                MediaType = "application/vnd.oci.image.manifest.v1+json",
                Digest = "sha256:abc123",
                Size = 1234
            });

        var mockLogger = new Mock<ILoggerService>();

        var generator = new SigningRequestGenerator(mockDescriptorService.Object, mockLogger.Object);

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

        var requests = await generator.GeneratePlatformSigningRequestsAsync(imageArtifactDetails);

        requests.Count.ShouldBe(1);
        requests[0].ImageName.ShouldBe("myregistry.azurecr.io/dotnet/runtime@sha256:abc123");
    }

    [Fact]
    public async Task GenerateManifestListSigningRequestsAsync_ReturnsManifestListRequests()
    {
        var mockDescriptorService = new Mock<IOrasDescriptorService>();
        mockDescriptorService
            .Setup(s => s.GetDescriptorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string reference, CancellationToken _) => new Descriptor
            {
                MediaType = "application/vnd.oci.image.index.v1+json",
                Digest = reference.Split('@')[1],
                Size = 5678
            });

        var mockLogger = new Mock<ILoggerService>();

        var generator = new SigningRequestGenerator(mockDescriptorService.Object, mockLogger.Object);

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

        var requests = await generator.GenerateManifestListSigningRequestsAsync(imageArtifactDetails);

        requests.Count.ShouldBe(1);
        requests[0].ImageName.ShouldBe("myregistry.azurecr.io/dotnet/runtime@sha256:manifest123");
        requests[0].Payload.TargetArtifact.MediaType.ShouldBe("application/vnd.oci.image.index.v1+json");
    }

    [Fact]
    public async Task GenerateManifestListSigningRequestsAsync_SkipsImagesWithoutManifest()
    {
        var mockDescriptorService = new Mock<IOrasDescriptorService>();
        var mockLogger = new Mock<ILoggerService>();

        var generator = new SigningRequestGenerator(mockDescriptorService.Object, mockLogger.Object);

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
                            Platforms = [new PlatformData { Digest = "sha256:abc123" }]
                        }
                    ]
                }
            ]
        };

        var requests = await generator.GenerateManifestListSigningRequestsAsync(imageArtifactDetails);

        requests.Count.ShouldBe(0);
        mockDescriptorService.Verify(
            s => s.GetDescriptorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GeneratePlatformSigningRequestsAsync_ReturnsEmptyForEmptyInput()
    {
        var mockDescriptorService = new Mock<IOrasDescriptorService>();
        var mockLogger = new Mock<ILoggerService>();

        var generator = new SigningRequestGenerator(mockDescriptorService.Object, mockLogger.Object);

        var imageArtifactDetails = new ImageArtifactDetails { Repos = [] };

        var requests = await generator.GeneratePlatformSigningRequestsAsync(imageArtifactDetails);

        requests.ShouldBeEmpty();
    }
}
