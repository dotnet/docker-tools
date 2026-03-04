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
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Commands.Signing;

public class VerifySignaturesCommandTests
{
    private const string ImageInfoPath = "/data/image-info.json";
    private const string TrustBasePath = "/notation-trust";
    private const string TrustStoreName = "test";
    private const string PlatformDigestA = "registry.io/repo@sha256:aaa";
    private const string PlatformDigestB = "registry.io/repo@sha256:bbb";
    private const string ManifestDigest = "registry.io/repo@sha256:ccc";

    private static readonly string s_certPath = Path.Combine(TrustBasePath, "certs", TrustStoreName, "root-ca.crt");
    private static readonly string s_policyPath = Path.Combine(TrustBasePath, "policies", $"{TrustStoreName}.json");

    /// <summary>Creates a default enabled signing configuration for tests.</summary>
    private static SigningConfiguration CreateDefaultSigningConfig()
    {
        return new SigningConfiguration()
        {
            Enabled = true,
            TrustStoreName = TrustStoreName
        };
    }

    /// <summary>
    /// Image-info JSON with two platform digests and one manifest list digest.
    /// </summary>
    private static readonly string s_imageInfoJson = $$"""
        {
          "repos": [
            {
              "repo": "dotnet/image-builder",
              "images": [
                {
                  "platforms": [
                    {
                      "digest": "{{PlatformDigestA}}",
                      "dockerfile": "Dockerfile",
                      "simpleTags": ["tag1"],
                      "osType": "Linux",
                      "osVersion": "ubuntu22.04",
                      "architecture": "amd64",
                      "created": "2026-01-01T00:00:00Z",
                      "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123"
                    },
                    {
                      "digest": "{{PlatformDigestB}}",
                      "dockerfile": "Dockerfile.windows",
                      "simpleTags": ["tag2"],
                      "osType": "Windows",
                      "osVersion": "nanoserver-ltsc2022",
                      "architecture": "amd64",
                      "created": "2026-01-01T00:00:00Z",
                      "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123"
                    }
                  ],
                  "manifest": {
                    "digest": "{{ManifestDigest}}",
                    "sharedTags": ["latest"]
                  }
                }
              ]
            }
          ]
        }
        """;

    /// <summary>
    /// Image-info JSON where all platform digests are empty, used to test the no-digests early exit.
    /// </summary>
    private static readonly string s_emptyDigestsImageInfoJson = """
        {
          "repos": [
            {
              "repo": "dotnet/image-builder",
              "images": [
                {
                  "platforms": [
                    {
                      "digest": "",
                      "dockerfile": "Dockerfile",
                      "simpleTags": ["tag1"],
                      "osType": "Linux",
                      "osVersion": "ubuntu22.04",
                      "architecture": "amd64",
                      "created": "2026-01-01T00:00:00Z",
                      "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task VerifySignatures_VerifiesAllImages()
    {
        TestContext testContext = CreateSeededCommand();

        await testContext.Command.ExecuteAsync();

        testContext.NotationClientMock.Verify(
            x => x.AddCertificate("ca", TrustStoreName, It.IsAny<string>()),
            Times.Once);
        testContext.NotationClientMock.Verify(
            x => x.ImportTrustPolicy(It.IsAny<string>()),
            Times.Once);
        testContext.NotationClientMock.Verify(
            x => x.Verify(PlatformDigestA, false),
            Times.Once);
        testContext.NotationClientMock.Verify(
            x => x.Verify(PlatformDigestB, false),
            Times.Once);
        testContext.NotationClientMock.Verify(
            x => x.Verify(ManifestDigest, false),
            Times.Once);
        testContext.NotationClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VerifySignatures_DoesNotSetExitCodeOnSuccess()
    {
        TestContext testContext = CreateSeededCommand();
        await testContext.Command.ExecuteAsync();
        testContext.EnvironmentServiceMock.VerifySet(x => x.ExitCode = It.IsAny<int>(), Times.Never);
    }

    [Fact]
    public async Task VerifySignatures_FailsWhenVerificationFails()
    {
        TestContext testContext = CreateSeededCommand();

        testContext.NotationClientMock
            .Setup(x => x.Verify(PlatformDigestB, false))
            .Throws(new InvalidOperationException("Verification failed"));

        await testContext.Command.ExecuteAsync();

        testContext.EnvironmentServiceMock.VerifySet(x => x.ExitCode = 1, Times.Once);
        testContext.NotationClientMock.Verify(x => x.Verify(PlatformDigestA, false), Times.Once);
        testContext.NotationClientMock.Verify(x => x.Verify(PlatformDigestB, false), Times.Once);
        testContext.NotationClientMock.Verify(x => x.Verify(ManifestDigest, false), Times.Once);
    }

    [Fact]
    public async Task VerifySignatures_SetsExitCodeOnceForMultipleFailures()
    {
        TestContext testContext = CreateSeededCommand();
        testContext.NotationClientMock.Setup(x => x.Verify(PlatformDigestA, false))
            .Throws(new InvalidOperationException("fail 1"));
        testContext.NotationClientMock.Setup(x => x.Verify(PlatformDigestB, false))
            .Throws(new InvalidOperationException("fail 2"));

        await testContext.Command.ExecuteAsync();

        testContext.EnvironmentServiceMock.VerifySet(x => x.ExitCode = 1, Times.Once);
        testContext.NotationClientMock.Verify(x => x.Verify(ManifestDigest, false), Times.Once);
    }

    [Fact]
    public async Task VerifySignatures_SkipsWhenSigningDisabled()
    {
        var notationClientMock = new Mock<INotationClient>();
        var signingConfig = new SigningConfiguration
        {
            Enabled = false,
            TrustStoreName = TrustStoreName
        };

        TestContext testContext = CreateCommand(notationClientMock.Object, signingConfig);
        await testContext.Command.ExecuteAsync();
        notationClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VerifySignatures_SkipsWhenSigningConfigNull()
    {
        var notationClientMock = new Mock<INotationClient>();
        TestContext testContext = CreateCommand(notationClientMock.Object, signingConfig: null);

        await testContext.Command.ExecuteAsync();

        notationClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VerifySignatures_SkipsWhenImageInfoMissing()
    {
        var notationClientMock = new Mock<INotationClient>();
        TestContext testContext = CreateCommand(notationClientMock.Object, CreateDefaultSigningConfig());
        testContext.Command.Options.ImageInfoPath = "/nonexistent/path/image-info.json";

        await testContext.Command.ExecuteAsync();

        notationClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VerifySignatures_SkipsWhenDryRun()
    {
        TestContext testContext = CreateSeededCommand();
        testContext.Command.Options.IsDryRun = true;

        await testContext.Command.ExecuteAsync();

        testContext.NotationClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VerifySignatures_SkipsWhenNoDigests()
    {
        var fileSystem = new InMemoryFileSystem();
        fileSystem.AddFile(ImageInfoPath, s_emptyDigestsImageInfoJson);
        SeedTrustMaterials(fileSystem);

        var notationClientMock = new Mock<INotationClient>();
        TestContext testContext = CreateCommand(notationClientMock.Object, CreateDefaultSigningConfig(), fileSystem);
        testContext.Command.Options.ImageInfoPath = ImageInfoPath;

        await testContext.Command.ExecuteAsync();

        notationClientMock.Verify(
            x => x.Verify(It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task VerifySignatures_ThrowsWhenCertMissing()
    {
        var fileSystem = new InMemoryFileSystem();
        SeedImageInfoFile(fileSystem);
        // Only seed the policy, not the cert
        SeedPolicy(fileSystem);

        var notationClientMock = new Mock<INotationClient>();
        TestContext testContext = CreateCommand(notationClientMock.Object, CreateDefaultSigningConfig(), fileSystem);
        testContext.Command.Options.ImageInfoPath = ImageInfoPath;

        FileNotFoundException exception = await Should.ThrowAsync<FileNotFoundException>(testContext.Command.ExecuteAsync());
        exception.Message.ShouldContain("Root CA certificate not found");
    }

    [Fact]
    public async Task VerifySignatures_ThrowsWhenTrustPolicyMissing()
    {
        var fileSystem = new InMemoryFileSystem();
        SeedImageInfoFile(fileSystem);
        // Only seed the cert, not the policy
        SeedCert(fileSystem);

        var notationClientMock = new Mock<INotationClient>();
        TestContext testContext = CreateCommand(notationClientMock.Object, CreateDefaultSigningConfig(), fileSystem);
        testContext.Command.Options.ImageInfoPath = ImageInfoPath;

        FileNotFoundException exception = await Should.ThrowAsync<FileNotFoundException>(testContext.Command.ExecuteAsync());
        exception.Message.ShouldContain("Trust policy not found");
    }

    /// <summary>
    /// Creates a fully seeded command with image-info and trust materials ready for verification tests.
    /// </summary>
    private static TestContext CreateSeededCommand()
    {
        var fileSystem = new InMemoryFileSystem();
        SeedImageInfoFile(fileSystem);
        SeedTrustMaterials(fileSystem);

        var notationClientMock = new Mock<INotationClient>();
        TestContext testContext = CreateCommand(notationClientMock.Object, CreateDefaultSigningConfig(), fileSystem);
        testContext.Command.Options.ImageInfoPath = ImageInfoPath;
        return testContext;
    }

    /// <summary>
    /// Creates a <see cref="VerifySignaturesCommand"/> with the specified signing configuration and file system.
    /// Pass null for <paramref name="signingConfig"/> to simulate a missing signing configuration.
    /// </summary>
    private static TestContext CreateCommand(
        INotationClient notationClient,
        SigningConfiguration? signingConfig,
        IFileSystem? fileSystem = null)
    {
        ILogger<VerifySignaturesCommand> logger = Mock.Of<ILogger<VerifySignaturesCommand>>();
        IRegistryCredentialsProvider credentialsProvider = Mock.Of<IRegistryCredentialsProvider>();
        var environmentServiceMock = new Mock<IEnvironmentService>();
        var notationClientMock = Mock.Get(notationClient);
        InMemoryFileSystem inMemoryFileSystem = fileSystem as InMemoryFileSystem ?? new InMemoryFileSystem();

        PublishConfiguration publishConfig = new() { Signing = signingConfig };
        IOptions<PublishConfiguration> publishOptions = Options.Create(publishConfig);
        VerifySignaturesCommand command = new(
            logger,
            notationClient,
            credentialsProvider,
            environmentServiceMock.Object,
            inMemoryFileSystem,
            publishOptions
        );

        command.Options.TrustMaterialsPath = TrustBasePath;
        return new TestContext(command, notationClientMock, environmentServiceMock, inMemoryFileSystem);
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
        SeedCert(fileSystem);
        SeedPolicy(fileSystem);
    }

    /// <summary>Seeds the root CA certificate into the in-memory file system.</summary>
    private static void SeedCert(InMemoryFileSystem fileSystem) =>
        fileSystem.AddFile(s_certPath, "fake-cert-data");

    /// <summary>Seeds the trust policy file into the in-memory file system.</summary>
    private static void SeedPolicy(InMemoryFileSystem fileSystem) =>
        fileSystem.AddFile(s_policyPath, """{"version":"1.0","trustPolicies":[]}""");

    /// <summary>
    /// Holds the command under test and its mocked dependencies for assertion.
    /// </summary>
    private record TestContext(
        VerifySignaturesCommand Command,
        Mock<INotationClient> NotationClientMock,
        Mock<IEnvironmentService> EnvironmentServiceMock,
        InMemoryFileSystem FileSystem);
}
