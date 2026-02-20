// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Options;
using Moq;
using Microsoft.Extensions.Logging;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class SignImagesCommandTests
{
    private const string ImageInfoPath = "/data/image-info.json";

    [Fact]
    public async Task ExecuteAsync_SigningDisabled_SkipsSigning()
    {
        var mockSigning = new Mock<IBulkImageSigningService>();
        var signingConfig = new SigningConfiguration { Enabled = false };
        var command = CreateCommand(mockSigning: mockSigning, signingConfig: signingConfig);

        await command.ExecuteAsync();

        mockSigning.Verify(
            s => s.SignImagesAsync(It.IsAny<IEnumerable<ImageSigningRequest>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SigningConfigNull_SkipsSigning()
    {
        var mockSigning = new Mock<IBulkImageSigningService>();
        var command = CreateCommand(mockSigning: mockSigning, signingConfig: null);

        await command.ExecuteAsync();

        mockSigning.Verify(
            s => s.SignImagesAsync(It.IsAny<IEnumerable<ImageSigningRequest>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ImageInfoNotFound_SkipsSigning()
    {
        var mockSigning = new Mock<IBulkImageSigningService>();
        var signingConfig = new SigningConfiguration { Enabled = true };
        var command = CreateCommand(mockSigning: mockSigning, signingConfig: signingConfig);
        command.Options.ImageInfoPath = "/nonexistent/path/image-info.json";

        await command.ExecuteAsync();

        mockSigning.Verify(
            s => s.SignImagesAsync(It.IsAny<IEnumerable<ImageSigningRequest>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_SkipsSigning()
    {
        var fileSystem = new InMemoryFileSystem();
        SeedImageInfoFile(fileSystem);

        var mockSigning = new Mock<IBulkImageSigningService>();
        var signingConfig = new SigningConfiguration { Enabled = true };
        var command = CreateCommand(mockSigning: mockSigning, signingConfig: signingConfig, fileSystem: fileSystem);
        command.Options.ImageInfoPath = ImageInfoPath;
        command.Options.IsDryRun = true;

        await command.ExecuteAsync();

        mockSigning.Verify(
            s => s.SignImagesAsync(It.IsAny<IEnumerable<ImageSigningRequest>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SignsAllPlatformsAndManifestLists()
    {
        var fileSystem = new InMemoryFileSystem();
        SeedImageInfoFile(fileSystem);

        var signingConfig = new SigningConfiguration
        {
            Enabled = true,
            ImageSigningKeyCode = 42
        };

        var platformRequest = CreateRequest("registry.io/repo@sha256:platform1");
        var manifestRequest = CreateRequest("registry.io/repo@sha256:manifest1");

        var mockRequestGen = new Mock<ISigningRequestGenerator>();
        mockRequestGen
            .Setup(g => g.GenerateSigningRequestsAsync(It.IsAny<ImageArtifactDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImageSigningRequest> { platformRequest, manifestRequest });

        var mockSigning = new Mock<IBulkImageSigningService>();
        mockSigning
            .Setup(s => s.SignImagesAsync(It.IsAny<IEnumerable<ImageSigningRequest>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImageSigningResult>
            {
                new("registry.io/repo@sha256:platform1", "sha256:sig1"),
                new("registry.io/repo@sha256:manifest1", "sha256:sig2")
            });

        var command = CreateCommand(
            mockSigning: mockSigning,
            mockRequestGen: mockRequestGen,
            signingConfig: signingConfig,
            fileSystem: fileSystem);
        command.Options.ImageInfoPath = ImageInfoPath;

        await command.ExecuteAsync();

        mockSigning.Verify(
            s => s.SignImagesAsync(
                It.Is<IEnumerable<ImageSigningRequest>>(reqs => reqs.Count() == 2),
                42,
                It.IsAny<CancellationToken>()),
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
    /// Seeds a minimal image-info.json into the in-memory file system.
    /// </summary>
    private static void SeedImageInfoFile(InMemoryFileSystem fileSystem)
    {
        var imageArtifactDetails = new ImageArtifactDetails
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "repo",
                    Images =
                    [
                        new ImageData
                        {
                            Platforms =
                            [
                                new PlatformData { Digest = "sha256:platform1" }
                            ],
                            Manifest = new ManifestData { Digest = "sha256:manifest1" }
                        }
                    ]
                }
            ]
        };

        fileSystem.AddFile(ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));
    }

    private static SignImagesCommand CreateCommand(
        Mock<IBulkImageSigningService>? mockSigning = null,
        Mock<ISigningRequestGenerator>? mockRequestGen = null,
        SigningConfiguration? signingConfig = null,
        IFileSystem? fileSystem = null)
    {
        var publishConfig = new PublishConfiguration { Signing = signingConfig };

        return new SignImagesCommand(
            Mock.Of<ILogger<SignImagesCommand>>(),
            (mockSigning ?? new Mock<IBulkImageSigningService>()).Object,
            (mockRequestGen ?? new Mock<ISigningRequestGenerator>()).Object,
            fileSystem ?? new InMemoryFileSystem(),
            Options.Create(publishConfig));
    }
}
