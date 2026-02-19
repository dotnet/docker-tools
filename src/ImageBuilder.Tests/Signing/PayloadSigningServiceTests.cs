// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.Extensions.Options;
using Moq;
using Microsoft.Extensions.Logging;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class PayloadSigningServiceTests
{
    private const string ArtifactStagingDir = "/artifacts/staging";

    [Fact]
    public async Task SignPayloadsAsync_EmptyRequests_ReturnsEmpty()
    {
        var service = CreateService();

        var results = await service.SignPayloadsAsync([], signingKeyCode: 100);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SignPayloadsAsync_WritesPayloadsToDisk()
    {
        var mockFileSystem = new Mock<IFileSystem>();
        mockFileSystem
            .Setup(fs => fs.CreateDirectory(It.IsAny<string>()))
            .Returns(new DirectoryInfo(ArtifactStagingDir));

        var mockEsrp = new Mock<IEsrpSigningService>();
        var request = CreateRequest("registry.io/repo@sha256:abc123");

        // CertificateChainCalculator will be called after signing — mock the file read
        mockFileSystem
            .Setup(fs => fs.ReadAllBytes(It.IsAny<string>()))
            .Returns(CreateMinimalCoseSign1Bytes());

        var service = CreateService(mockEsrp: mockEsrp, mockFileSystem: mockFileSystem);

        await service.SignPayloadsAsync([request], signingKeyCode: 100);

        mockFileSystem.Verify(
            fs => fs.WriteAllText(
                It.Is<string>(path => path.Contains("sha256-abc123") && path.EndsWith(".payload")),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SignPayloadsAsync_CallsEsrpWithCorrectFilePaths()
    {
        var mockFileSystem = new Mock<IFileSystem>();
        mockFileSystem
            .Setup(fs => fs.CreateDirectory(It.IsAny<string>()))
            .Returns(new DirectoryInfo(ArtifactStagingDir));
        mockFileSystem
            .Setup(fs => fs.ReadAllBytes(It.IsAny<string>()))
            .Returns(CreateMinimalCoseSign1Bytes());

        var mockEsrp = new Mock<IEsrpSigningService>();
        var request = CreateRequest("registry.io/repo@sha256:abc123");

        var service = CreateService(mockEsrp: mockEsrp, mockFileSystem: mockFileSystem);

        await service.SignPayloadsAsync([request], signingKeyCode: 42);

        mockEsrp.Verify(
            e => e.SignFilesAsync(
                It.Is<IEnumerable<string>>(paths => paths.Count() == 1),
                42,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SignPayloadsAsync_MissingArtifactStagingDir_ThrowsInvalidOperation()
    {
        var buildConfig = new BuildConfiguration { ArtifactStagingDirectory = "" };
        var service = CreateService(buildConfig: buildConfig);

        var request = CreateRequest("registry.io/repo@sha256:abc123");

        var ex = await Should.ThrowAsync<System.InvalidOperationException>(
            () => service.SignPayloadsAsync([request], signingKeyCode: 100));

        ex.Message.ShouldContain("ArtifactStagingDirectory");
    }

    [Fact]
    public async Task SignPayloadsAsync_SanitizesDigestForFilename()
    {
        var mockFileSystem = new Mock<IFileSystem>();
        mockFileSystem
            .Setup(fs => fs.CreateDirectory(It.IsAny<string>()))
            .Returns(new DirectoryInfo(ArtifactStagingDir));
        mockFileSystem
            .Setup(fs => fs.ReadAllBytes(It.IsAny<string>()))
            .Returns(CreateMinimalCoseSign1Bytes());

        var request = CreateRequest("registry.io/repo@sha256:abc123");
        var service = CreateService(mockFileSystem: mockFileSystem);

        await service.SignPayloadsAsync([request], signingKeyCode: 100);

        // Verify the filename replaces ":" with "-"
        mockFileSystem.Verify(
            fs => fs.WriteAllText(
                It.Is<string>(path => path.Contains("sha256-abc123.payload")),
                It.IsAny<string>()),
            Times.Once);
    }

    private static ImageSigningRequest CreateRequest(string imageName)
    {
        var descriptor = new Descriptor(
            "application/vnd.oci.image.manifest.v1+json",
            "sha256:abc123",
            1234);
        var payload = new Payload(descriptor);
        var orasDescriptor = OrasDescriptor.Create([], "application/vnd.oci.image.manifest.v1+json");
        return new ImageSigningRequest(imageName, orasDescriptor, payload);
    }

    /// <summary>
    /// Creates minimal valid COSE_Sign1 bytes with a single certificate in the x5chain,
    /// used by CertificateChainCalculator when reading signed payloads.
    /// </summary>
    private static byte[] CreateMinimalCoseSign1Bytes()
    {
        var writer = new System.Formats.Cbor.CborWriter();
        writer.WriteTag((System.Formats.Cbor.CborTag)18); // COSE_Sign1

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

    private static PayloadSigningService CreateService(
        Mock<IEsrpSigningService>? mockEsrp = null,
        Mock<IFileSystem>? mockFileSystem = null,
        BuildConfiguration? buildConfig = null)
    {
        buildConfig ??= new BuildConfiguration { ArtifactStagingDirectory = ArtifactStagingDir };

        var fileSystem = mockFileSystem ?? new Mock<IFileSystem>();
        fileSystem
            .Setup(fs => fs.CreateDirectory(It.IsAny<string>()))
            .Returns(new DirectoryInfo(ArtifactStagingDir));

        return new PayloadSigningService(
            (mockEsrp ?? new Mock<IEsrpSigningService>()).Object,
            Mock.Of<ILogger<PayloadSigningService>>(),
            fileSystem.Object,
            Options.Create(buildConfig));
    }
}
