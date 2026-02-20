// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.DotNet.ImageBuilder.Signing;
using Moq;
using Microsoft.Extensions.Logging;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class ImageSigningServiceTests
{
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
        var mockPayloadSigning = CreateMockPayloadSigning();

        var service = CreateService(mockOras, mockPayloadSigning);

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

        var subjectDescriptor = OrasDescriptor.Create([], "application/vnd.oci.image.manifest.v1+json");
        var signedPayload = new PayloadSigningResult(
            "myregistry.azurecr.io/dotnet/runtime@sha256:abc123",
            subjectDescriptor,
            "/tmp/signed.cose",
            "[\"thumbprint1\"]");

        var mockPayloadSigning = new Mock<IPayloadSigningService>();
        mockPayloadSigning
            .Setup(s => s.SignPayloadsAsync(
                It.IsAny<IEnumerable<ImageSigningRequest>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([signedPayload]);

        mockOras
            .Setup(s => s.PushSignatureAsync(
                It.IsAny<OrasDescriptor>(), It.IsAny<PayloadSigningResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sha256:sigdigest");

        var service = CreateService(mockOras, mockPayloadSigning);

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
        mockPayloadSigning.Verify(
            s => s.SignPayloadsAsync(It.IsAny<IEnumerable<ImageSigningRequest>>(), 42, It.IsAny<CancellationToken>()),
            Times.Once);
        mockOras.Verify(
            s => s.PushSignatureAsync(It.IsAny<OrasDescriptor>(), signedPayload, It.IsAny<CancellationToken>()),
            Times.Once);

        results.Count.ShouldBe(1);
        results[0].SignatureDigest.ShouldBe("sha256:sigdigest");
    }

    [Fact]
    public async Task SignImagesAsync_ResolvesPlatformAndManifestListDigests()
    {
        var mockOras = CreateMockOrasService();
        var mockPayloadSigning = CreateMockPayloadSigning();

        var service = CreateService(mockOras, mockPayloadSigning);

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
    public async Task SignImagesAsync_BuildsCorrectPayload()
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

        var capturedRequests = new List<ImageSigningRequest>();
        var mockPayloadSigning = new Mock<IPayloadSigningService>();
        mockPayloadSigning
            .Setup(s => s.SignPayloadsAsync(
                It.IsAny<IEnumerable<ImageSigningRequest>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ImageSigningRequest>, int, CancellationToken>(
                (reqs, _, _) => capturedRequests.AddRange(reqs))
            .ReturnsAsync([]);

        var service = CreateService(mockOras, mockPayloadSigning);

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

        capturedRequests.Count.ShouldBe(1);
        capturedRequests[0].ImageName.ShouldBe("sha256:manifest123");
        capturedRequests[0].Payload.TargetArtifact.MediaType.ShouldBe("application/vnd.oci.image.index.v1+json");
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

    private static Mock<IPayloadSigningService> CreateMockPayloadSigning()
    {
        var mock = new Mock<IPayloadSigningService>();
        var subjectDescriptor = OrasDescriptor.Create([], "application/vnd.oci.image.manifest.v1+json");
        mock
            .Setup(s => s.SignPayloadsAsync(
                It.IsAny<IEnumerable<ImageSigningRequest>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ImageSigningRequest> reqs, int _, CancellationToken _) =>
                reqs.Select(r => new PayloadSigningResult(
                    r.ImageName, subjectDescriptor, "/tmp/signed.cose", "[\"thumbprint1\"]")).ToList());
        return mock;
    }

    private static ImageSigningService CreateService(
        Mock<IOrasService>? mockOras = null,
        Mock<IPayloadSigningService>? mockPayloadSigning = null)
    {
        return new ImageSigningService(
            (mockOras ?? new Mock<IOrasService>()).Object,
            (mockPayloadSigning ?? new Mock<IPayloadSigningService>()).Object,
            Mock.Of<ILogger<ImageSigningService>>());
    }
}
