// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder;
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
    [Fact]
    public async Task VerifySignatures_VerifiesAllImages()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        var imageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
        
        var imageInfoJson = """
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
        
        File.WriteAllText(imageInfoPath, imageInfoJson);
        var trustBasePath = CreateTrustMaterials(tempFolderContext.Path);
        
        var notationClientMock = new Mock<INotationClient>();
        var command = CreateCommand(notationClientMock.Object, trustBasePath: trustBasePath);
        command.Options.ImageInfoPath = imageInfoPath;
        
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
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        var imageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
        
        var imageInfoJson = """
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
        
        File.WriteAllText(imageInfoPath, imageInfoJson);
        var trustBasePath = CreateTrustMaterials(tempFolderContext.Path);
        
        var notationClientMock = new Mock<INotationClient>();
        notationClientMock.Setup(x => x.Verify("registry.io/repo@sha256:bbb", false))
            .Throws(new InvalidOperationException("Verification failed"));
        
        var command = CreateCommand(notationClientMock.Object, trustBasePath: trustBasePath);
        command.Options.ImageInfoPath = imageInfoPath;
        
        var exception = await Should.ThrowAsync<InvalidOperationException>(command.ExecuteAsync());
        exception.Message.ShouldContain("Signature verification failed for 1 of 3 image(s)");
        
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
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        var imageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
        
        var imageInfoJson = """
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
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
        
        File.WriteAllText(imageInfoPath, imageInfoJson);
        
        var notationClientMock = new Mock<INotationClient>();
        var command = CreateCommand(notationClientMock.Object, signingEnabled: false);
        command.Options.ImageInfoPath = imageInfoPath;
        
        await command.ExecuteAsync();
        
        notationClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VerifySignatures_SkipsWhenImageInfoMissing()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        var imageInfoPath = Path.Combine(tempFolderContext.Path, "non-existent.json");
        
        var notationClientMock = new Mock<INotationClient>();
        var command = CreateCommand(notationClientMock.Object);
        command.Options.ImageInfoPath = imageInfoPath;
        
        await command.ExecuteAsync();
        
        notationClientMock.VerifyNoOtherCalls();
    }

    private static VerifySignaturesCommand CreateCommand(
        INotationClient notationClient,
        bool signingEnabled = true,
        string? trustBasePath = null)
    {
        var logger = Mock.Of<ILogger<VerifySignaturesCommand>>();
        var credentialsProvider = Mock.Of<IRegistryCredentialsProvider>();
        var publishConfig = new PublishConfiguration
        {
            Signing = new SigningConfiguration
            {
                Enabled = signingEnabled,
                TrustStoreName = "test"
            }
        };
        var options = Microsoft.Extensions.Options.Options.Create(publishConfig);
        
        var command = new VerifySignaturesCommand(logger, notationClient, credentialsProvider, options);
        if (trustBasePath is not null)
        {
            command.Options.TrustMaterialsPath = trustBasePath;
        }
        return command;
    }

    /// <summary>
    /// Creates the trust materials directory structure expected by VerifySignaturesCommand.
    /// </summary>
    private static string CreateTrustMaterials(string basePath)
    {
        var trustPath = Path.Combine(basePath, "notation-trust");
        var certDir = Path.Combine(trustPath, "certs", "test");
        var policyDir = Path.Combine(trustPath, "policies");
        
        Directory.CreateDirectory(certDir);
        Directory.CreateDirectory(policyDir);
        
        File.WriteAllText(Path.Combine(certDir, "root-ca.crt"), "fake-cert-data");
        File.WriteAllText(Path.Combine(policyDir, "test.json"), """{"version":"1.0","trustPolicies":[]}""");
        
        return trustPath;
    }
}
