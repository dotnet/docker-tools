// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Notation;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Commands.Signing;

public class VerifySignaturesCommandTests
{
    private const string ImageInfoPath = "/data/image-info.json";
    private const string TrustBasePath = "/notation-trust";

    private static readonly string s_imageInfoJson = """
        {
          "repos": [
            {
              "repo": "dotnet/image-builder",
              "images": [
                {
                  "platforms": [
                    {
                      "digest": "registry.io/repo@sha256:aaa",
                      "dockerfile": "Dockerfile",
                      "simpleTags": ["tag1"],
                      "osType": "Linux",
                      "osVersion": "ubuntu22.04",
                      "architecture": "amd64",
                      "created": "2024-01-01T00:00:00Z",
                      "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123"
                    },
                    {
                      "digest": "registry.io/repo@sha256:bbb",
                      "dockerfile": "Dockerfile.windows",
                      "simpleTags": ["tag2"],
                      "osType": "Windows",
                      "osVersion": "nanoserver-ltsc2022",
                      "architecture": "amd64",
                      "created": "2024-01-01T00:00:00Z",
                      "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123"
                    }
                  ],
                  "manifest": {
                    "digest": "registry.io/repo@sha256:ccc",
                    "sharedTags": ["latest"]
                  }
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task VerifySignatures_VerifiesAllImages()
    {
        var fileSystem = new InMemoryFileSystem();
        SeedImageInfoFile(fileSystem);
        SeedTrustMaterials(fileSystem);

        var notationClientMock = new Mock<INotationClient>();
        var (command, _) = CreateCommand(notationClientMock.Object, fileSystem: fileSystem);
        command.Options.ImageInfoPath = ImageInfoPath;

        await command.ExecuteAsync();

        notationClientMock.Verify(
            x => x.AddCertificate("ca", "test", It.IsAny<string>()),
            Times.Once);
        notationClientMock.Verify(
            x => x.ImportTrustPolicy(It.IsAny<string>()),
            Times.Once);
        notationClientMock.Verify(
            x => x.Verify("registry.io/repo@sha256:aaa", false),
            Times.Once);
        notationClientMock.Verify(
            x => x.Verify("registry.io/repo@sha256:bbb", false),
            Times.Once);
        notationClientMock.Verify(
            x => x.Verify("registry.io/repo@sha256:ccc", false),
            Times.Once);
        notationClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VerifySignatures_FailsWhenVerificationFails()
    {
        var fileSystem = new InMemoryFileSystem();
        SeedImageInfoFile(fileSystem);
        SeedTrustMaterials(fileSystem);

        var notationClientMock = new Mock<INotationClient>();
        notationClientMock.Setup(x => x.Verify("registry.io/repo@sha256:bbb", false))
            .Throws(new InvalidOperationException("Verification failed"));

        var (command, environmentServiceMock) = CreateCommand(notationClientMock.Object, fileSystem: fileSystem);
        command.Options.ImageInfoPath = ImageInfoPath;

        await command.ExecuteAsync();

        environmentServiceMock.VerifySet(x => x.ExitCode = 1, Times.Once);

        notationClientMock.Verify(
            x => x.Verify("registry.io/repo@sha256:aaa", false),
            Times.Once);
        notationClientMock.Verify(
            x => x.Verify("registry.io/repo@sha256:bbb", false),
            Times.Once);
        notationClientMock.Verify(
            x => x.Verify("registry.io/repo@sha256:ccc", false),
            Times.Once);
    }

    [Fact]
    public async Task VerifySignatures_SkipsWhenSigningDisabled()
    {
        var fileSystem = new InMemoryFileSystem();
        SeedImageInfoFile(fileSystem);

        var notationClientMock = new Mock<INotationClient>();
        var (command, _) = CreateCommand(notationClientMock.Object, signingEnabled: false, fileSystem: fileSystem);
        command.Options.ImageInfoPath = ImageInfoPath;

        await command.ExecuteAsync();

        notationClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VerifySignatures_SkipsWhenImageInfoMissing()
    {
        var fileSystem = new InMemoryFileSystem();

        var notationClientMock = new Mock<INotationClient>();
        var (command, _) = CreateCommand(notationClientMock.Object, fileSystem: fileSystem);
        command.Options.ImageInfoPath = "/nonexistent/path/image-info.json";

        await command.ExecuteAsync();

        notationClientMock.VerifyNoOtherCalls();
    }

    private static (VerifySignaturesCommand Command, Mock<IEnvironmentService> EnvironmentServiceMock) CreateCommand(
        INotationClient notationClient,
        bool signingEnabled = true,
        IFileSystem? fileSystem = null)
    {
        var logger = Mock.Of<ILogger<VerifySignaturesCommand>>();
        var credentialsProvider = Mock.Of<IRegistryCredentialsProvider>();
        var environmentServiceMock = new Mock<IEnvironmentService>();
        var publishConfig = new PublishConfiguration
        {
            Signing = new SigningConfiguration
            {
                Enabled = signingEnabled,
                TrustStoreName = "test"
            }
        };
        var options = Microsoft.Extensions.Options.Options.Create(publishConfig);

        var command = new VerifySignaturesCommand(
            logger, notationClient, credentialsProvider, environmentServiceMock.Object, fileSystem ?? new InMemoryFileSystem(), options);
        command.Options.TrustMaterialsPath = TrustBasePath;
        return (command, environmentServiceMock);
    }

    /// <summary>
    /// Seeds the image-info.json file into the in-memory file system.
    /// </summary>
    private static void SeedImageInfoFile(InMemoryFileSystem fileSystem) =>
        fileSystem.AddFile(ImageInfoPath, s_imageInfoJson);

    /// <summary>
    /// Seeds the trust materials (root CA certificate and trust policy) into the in-memory file system.
    /// </summary>
    private static void SeedTrustMaterials(InMemoryFileSystem fileSystem)
    {
        fileSystem.AddFile(
            Path.Combine(TrustBasePath, "certs", "test", "root-ca.crt"),
            "fake-cert-data");
        fileSystem.AddFile(
            Path.Combine(TrustBasePath, "policies", "test.json"),
            """{"version":"1.0","trustPolicies":[]}""");
    }
}
