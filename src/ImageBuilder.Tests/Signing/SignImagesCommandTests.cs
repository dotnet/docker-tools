// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
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
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class SignImagesCommandTests
{
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
        using var tempFolder = TestHelper.UseTempFolder();
        var imageInfoPath = WriteImageInfoFile(tempFolder.Path);

        var mockSigning = new Mock<IBulkImageSigningService>();
        var signingConfig = new SigningConfiguration { Enabled = true };
        var command = CreateCommand(mockSigning: mockSigning, signingConfig: signingConfig);
        command.Options.ImageInfoPath = imageInfoPath;
        command.Options.IsDryRun = true;

        await command.ExecuteAsync();

        mockSigning.Verify(
            s => s.SignImagesAsync(It.IsAny<IEnumerable<ImageSigningRequest>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SignsAllPlatformsAndManifestLists()
    {
        using var tempFolder = TestHelper.UseTempFolder();
        var imageInfoPath = WriteImageInfoFile(tempFolder.Path);

        var signingConfig = new SigningConfiguration
        {
            Enabled = true,
            ImageSigningKeyCode = 42
        };

        var platformRequest = CreateRequest("registry.io/repo@sha256:platform1");
        var manifestRequest = CreateRequest("registry.io/repo@sha256:manifest1");

        var mockRequestGen = new Mock<ISigningRequestGenerator>();
        mockRequestGen
            .Setup(g => g.GeneratePlatformSigningRequestsAsync(It.IsAny<ImageArtifactDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImageSigningRequest> { platformRequest });
        mockRequestGen
            .Setup(g => g.GenerateManifestListSigningRequestsAsync(It.IsAny<ImageArtifactDetails>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImageSigningRequest> { manifestRequest });

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
            signingConfig: signingConfig);
        command.Options.ImageInfoPath = imageInfoPath;

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
        return new ImageSigningRequest(imageName, payload);
    }

    /// <summary>
    /// Writes a minimal image-info.json file to the temp folder and returns its path.
    /// </summary>
    private static string WriteImageInfoFile(string directory)
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

        var path = Path.Combine(directory, "image-info.json");
        File.WriteAllText(path, JsonHelper.SerializeObject(imageArtifactDetails));
        return path;
    }

    private static SignImagesCommand CreateCommand(
        Mock<IBulkImageSigningService>? mockSigning = null,
        Mock<ISigningRequestGenerator>? mockRequestGen = null,
        SigningConfiguration? signingConfig = null)
    {
        var publishConfig = new PublishConfiguration { Signing = signingConfig };

        return new SignImagesCommand(
            Mock.Of<ILogger<SignImagesCommand>>(),
            (mockSigning ?? new Mock<IBulkImageSigningService>()).Object,
            (mockRequestGen ?? new Mock<ISigningRequestGenerator>()).Object,
            Options.Create(publishConfig));
    }
}
