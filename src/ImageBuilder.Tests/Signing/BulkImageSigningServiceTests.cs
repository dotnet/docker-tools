// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.DotNet.ImageBuilder.Signing;
using Moq;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class BulkImageSigningServiceTests
{
    [Fact]
    public async Task SignImagesAsync_EmptyRequests_ReturnsEmpty()
    {
        var service = CreateService();

        var results = await service.SignImagesAsync([], signingKeyCode: 100);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SignImagesAsync_OrchestratesSigningAndPush()
    {
        var mockPayloadSigning = new Mock<IPayloadSigningService>();
        var mockDescriptor = new Mock<IOrasDescriptorService>();
        var mockSignature = new Mock<IOrasSignatureService>();

        var request = CreateRequest("registry.io/repo@sha256:abc123");

        var signedPayload = new PayloadSigningResult(
            "registry.io/repo@sha256:abc123",
            new FileInfo("/tmp/signed.cose"),
            "[\"thumbprint1\"]");

        mockPayloadSigning
            .Setup(s => s.SignPayloadsAsync(
                It.IsAny<IEnumerable<ImageSigningRequest>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([signedPayload]);

        var subjectDescriptor = OrasDescriptor.Create([], "application/vnd.oci.image.manifest.v1+json");

        mockDescriptor
            .Setup(s => s.GetDescriptorAsync("registry.io/repo@sha256:abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subjectDescriptor);

        mockSignature
            .Setup(s => s.PushSignatureAsync(subjectDescriptor, signedPayload, It.IsAny<CancellationToken>()))
            .ReturnsAsync("sha256:sigdigest");

        var service = CreateService(mockPayloadSigning, mockDescriptor, mockSignature);

        await service.SignImagesAsync([request], signingKeyCode: 100);

        mockPayloadSigning.Verify(
            s => s.SignPayloadsAsync(It.IsAny<IEnumerable<ImageSigningRequest>>(), 100, It.IsAny<CancellationToken>()),
            Times.Once);
        mockDescriptor.Verify(
            s => s.GetDescriptorAsync("registry.io/repo@sha256:abc123", It.IsAny<CancellationToken>()),
            Times.Once);
        mockSignature.Verify(
            s => s.PushSignatureAsync(subjectDescriptor, signedPayload, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SignImagesAsync_ReturnsCorrectResults()
    {
        var mockPayloadSigning = new Mock<IPayloadSigningService>();
        var mockDescriptor = new Mock<IOrasDescriptorService>();
        var mockSignature = new Mock<IOrasSignatureService>();

        var request = CreateRequest("registry.io/repo@sha256:abc123");

        var signedPayload = new PayloadSigningResult(
            "registry.io/repo@sha256:abc123",
            new FileInfo("/tmp/signed.cose"),
            "[\"thumbprint1\"]");

        mockPayloadSigning
            .Setup(s => s.SignPayloadsAsync(
                It.IsAny<IEnumerable<ImageSigningRequest>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([signedPayload]);

        mockDescriptor
            .Setup(s => s.GetDescriptorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrasDescriptor.Create([], "application/vnd.oci.image.manifest.v1+json"));

        mockSignature
            .Setup(s => s.PushSignatureAsync(It.IsAny<OrasDescriptor>(), It.IsAny<PayloadSigningResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sha256:sigdigest");

        var service = CreateService(mockPayloadSigning, mockDescriptor, mockSignature);

        var results = await service.SignImagesAsync([request], signingKeyCode: 100);

        results.Count.ShouldBe(1);
        results[0].ImageName.ShouldBe("registry.io/repo@sha256:abc123");
        results[0].SignatureDigest.ShouldBe("sha256:sigdigest");
    }

    private static ImageSigningRequest CreateRequest(string imageName)
    {
        var descriptor = new Descriptor(
            "application/vnd.oci.image.manifest.v1+json",
            "sha256:abc123",
            1234);
        var payload = new Payload(descriptor);
        return new ImageSigningRequest(imageName, payload);
    }

    private static BulkImageSigningService CreateService(
        Mock<IPayloadSigningService>? mockPayloadSigning = null,
        Mock<IOrasDescriptorService>? mockDescriptor = null,
        Mock<IOrasSignatureService>? mockSignature = null)
    {
        return new BulkImageSigningService(
            (mockPayloadSigning ?? new Mock<IPayloadSigningService>()).Object,
            (mockDescriptor ?? new Mock<IOrasDescriptorService>()).Object,
            (mockSignature ?? new Mock<IOrasSignatureService>()).Object,
            Mock.Of<ILoggerService>());
    }
}
