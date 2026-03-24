#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Shouldly;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestServiceHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class CreateManifestListCommandTests
{
    /// <summary>
    /// Verifies that manifest lists are created, pushed, and digests are
    /// recorded in image-info.json.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CreatesAndPushesManifestLists()
    {
        Mock<IManifestService> manifestServiceMock = CreateManifestServiceMock();
        Mock<IManifestServiceFactory> manifestServiceFactory = CreateManifestServiceFactoryMock(manifestServiceMock);
        manifestServiceMock
            .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("digest1");

        Mock<IDockerService> dockerServiceMock = new();

        DateTime createdDate = DateTime.UtcNow;
        IDateTimeService dateTimeService = Mock.Of<IDateTimeService>(o => o.UtcNow == createdDate);

        CreateManifestListCommand command = CreateCommand(
            manifestServiceFactory, dockerServiceMock, dateTimeService);

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag1", "sharedtag2"],
                    CreatePlatform(dockerfile, ["tag1"]))));

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag1", "sharedtag2"],
                    CreatePlatform(dockerfile, simpleTags: ["tag1"]))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        await command.ExecuteAsync();

        // Verify manifest lists were created
        dockerServiceMock.Verify(o => o.CreateManifestList(
            "repo:sharedtag1", new[] { "repo:tag1" }, false));
        dockerServiceMock.Verify(o => o.CreateManifestList(
            "repo:sharedtag2", new[] { "repo:tag1" }, false));

        // Verify manifest lists were pushed
        dockerServiceMock.Verify(o => o.PushManifestList("repo:sharedtag1", false));
        dockerServiceMock.Verify(o => o.PushManifestList("repo:sharedtag2", false));
    }

    /// <summary>
    /// Verifies that ManifestData.Digest is populated from the registry after pushing.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RecordsDigestsInImageInfo()
    {
        Mock<IManifestService> manifestServiceMock = new() { CallBase = true };
        Mock<IManifestServiceFactory> manifestServiceFactory = CreateManifestServiceFactoryMock(manifestServiceMock);
        manifestServiceMock
            .Setup(o => o.GetManifestAsync(It.IsAny<ImageName>(), false))
            .ReturnsAsync(new ManifestQueryResult("digest-sha", new JsonObject()));

        DateTime createdDate = DateTime.UtcNow;
        IDateTimeService dateTimeService = Mock.Of<IDateTimeService>(o => o.UtcNow == createdDate);

        CreateManifestListCommand command = CreateCommand(
            manifestServiceFactory, new Mock<IDockerService>(), dateTimeService);

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag1", "sharedtag2"],
                    CreatePlatform(dockerfile, ["tag1"]))));

        const string platformDigest = "sha256:abc";
        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag1", "sharedtag2"],
                    CreatePlatform(dockerfile, digest: platformDigest, simpleTags: ["tag1"]))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        await command.ExecuteAsync();

        ImageArtifactDetails result = JsonConvert.DeserializeObject<ImageArtifactDetails>(
            File.ReadAllText(command.Options.ImageInfoPath));

        result.Repos[0].Images[0].Manifest.ShouldNotBeNull();
        result.Repos[0].Images[0].Manifest.Digest.ShouldBe("repo@digest-sha");
        result.Repos[0].Images[0].Manifest.Created.ShouldBe(createdDate);
    }

    /// <summary>
    /// Verifies that syndicated digests are recorded in ManifestData.SyndicatedDigests.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RecordsSyndicatedDigests()
    {
        Mock<IManifestService> manifestServiceMock = new() { CallBase = true };
        Mock<IManifestServiceFactory> manifestServiceFactory = CreateManifestServiceFactoryMock(manifestServiceMock);

        manifestServiceMock
            .Setup(o => o.GetManifestAsync(
                It.Is<ImageName>(i => i.ToString().Contains("repo:sharedtag")), false))
            .ReturnsAsync(new ManifestQueryResult("primary-digest", new JsonObject()));
        manifestServiceMock
            .Setup(o => o.GetManifestAsync(
                It.Is<ImageName>(i => i.ToString().Contains("syndicated-repo:syn-sharedtag")), false))
            .ReturnsAsync(new ManifestQueryResult("syndicated-digest", new JsonObject()));

        DateTime createdDate = DateTime.UtcNow;
        IDateTimeService dateTimeService = Mock.Of<IDateTimeService>(o => o.UtcNow == createdDate);

        CreateManifestListCommand command = CreateCommand(
            manifestServiceFactory, new Mock<IDockerService>(), dateTimeService);

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        Platform platform;
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    [platform = CreatePlatform(dockerfile, Array.Empty<string>())],
                    new Dictionary<string, Tag>
                    {
                        {
                            "sharedtag",
                            new Tag
                            {
                                Syndication = new TagSyndication
                                {
                                    Repo = "syndicated-repo",
                                    DestinationTags = ["syn-sharedtag"]
                                }
                            }
                        }
                    })));

        manifest.Registry = "mcr.microsoft.com";
        platform.Tags = new Dictionary<string, Tag>
        {
            {
                "tag1",
                new Tag
                {
                    Syndication = new TagSyndication
                    {
                        Repo = "syndicated-repo",
                        DestinationTags = ["syn-tag1"]
                    }
                }
            }
        };

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfile, simpleTags: ["tag1"]))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        await command.ExecuteAsync();

        ImageArtifactDetails result = JsonConvert.DeserializeObject<ImageArtifactDetails>(
            File.ReadAllText(command.Options.ImageInfoPath));

        ManifestData manifestData = result.Repos[0].Images[0].Manifest;
        manifestData.ShouldNotBeNull();
        manifestData.Digest.ShouldBe("mcr.microsoft.com/repo@primary-digest");
        manifestData.SyndicatedDigests.Count.ShouldBe(1);
        manifestData.SyndicatedDigests[0].ShouldBe("mcr.microsoft.com/syndicated-repo@syndicated-digest");
    }

    /// <summary>
    /// Verifies that when image-info file doesn't exist, command logs a warning and returns.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MissingImageInfoFile_LogsWarningAndReturns()
    {
        CreateManifestListCommand command = CreateCommand(
            CreateManifestServiceFactoryMock(CreateManifestServiceMock()),
            new Mock<IDockerService>(),
            Mock.Of<IDateTimeService>());

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag"],
                    CreatePlatform(dockerfile, ["tag1"]))));

        command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
        command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "nonexistent-image-info.json");
        File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));
        command.LoadManifest();

        // Should not throw
        await command.ExecuteAsync();

        // File should still not exist (command didn't create it)
        File.Exists(command.Options.ImageInfoPath).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that only platforms present in image-info are included in manifest lists,
    /// testing the full command end-to-end.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OnlyCreatesForBuiltPlatforms()
    {
        Mock<IManifestService> manifestServiceMock = CreateManifestServiceMock();
        Mock<IManifestServiceFactory> manifestServiceFactory = CreateManifestServiceFactoryMock(manifestServiceMock);
        manifestServiceMock
            .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("digest1");

        Mock<IDockerService> dockerServiceMock = new();

        CreateManifestListCommand command = CreateCommand(
            manifestServiceFactory, dockerServiceMock, Mock.Of<IDateTimeService>());

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfileAmd64 = CreateDockerfile("1.0/repo/linux-amd64", tempFolderContext);
        string dockerfileArm64 = CreateDockerfile("1.0/repo/linux-arm64", tempFolderContext);
        string dockerfileWindows = CreateDockerfile("1.0/repo/windows", tempFolderContext);

        // Manifest defines 3 platforms
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag"],
                    CreatePlatform(dockerfileAmd64, ["tag-amd64"]),
                    CreatePlatform(dockerfileArm64, ["tag-arm64"], architecture: Architecture.ARM64),
                    CreatePlatform(dockerfileWindows, ["tag-windows"], os: OS.Windows, osVersion: "ltsc2022"))));

        // Only 2 platforms were built
        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfileAmd64, simpleTags: ["tag-amd64"]),
                    CreatePlatform(dockerfileArm64, simpleTags: ["tag-arm64"], architecture: "arm64"))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        await command.ExecuteAsync();

        // Manifest list should only reference the 2 built platforms
        dockerServiceMock.Verify(o => o.CreateManifestList(
            "repo:sharedtag",
            It.Is<IEnumerable<string>>(images =>
                images.Contains("repo:tag-amd64") &&
                images.Contains("repo:tag-arm64") &&
                !images.Contains("repo:tag-windows")),
            false));
    }

    private static CreateManifestListCommand CreateCommand(
        Mock<IManifestServiceFactory> manifestServiceFactory,
        Mock<IDockerService> dockerServiceMock,
        IDateTimeService dateTimeService) =>
        new(
            TestHelper.CreateManifestJsonService(),
            manifestServiceFactory.Object,
            dockerServiceMock.Object,
            Mock.Of<ILogger<CreateManifestListCommand>>(),
            dateTimeService,
            Mock.Of<IRegistryCredentialsProvider>());

    private static void SetupCommand(
        CreateManifestListCommand command,
        Manifest manifest,
        ImageArtifactDetails imageArtifactDetails,
        TempFolderContext tempFolderContext)
    {
        command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
        command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

        File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));
        File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

        command.LoadManifest();
    }
}
