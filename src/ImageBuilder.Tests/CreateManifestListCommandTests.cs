#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Azure.ResourceManager.ContainerRegistry.Models;
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
    /// Verifies that when only some manifest-declared platforms were built this run,
    /// the missing sibling platforms are ported in from the source registry so the
    /// shared-tag manifest list still references all declared platforms.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PortsMissingPlatforms_IntoManifestList()
    {
        Mock<IManifestService> manifestServiceMock = CreateManifestServiceMock();
        Mock<IManifestServiceFactory> manifestServiceFactory = CreateManifestServiceFactoryMock(manifestServiceMock);
        manifestServiceMock
            .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("digest1");
        // The Windows platform is missing from image-info and gets ported from the
        // source registry; its previously-published digest is looked up here.
        manifestServiceMock
            .Setup(o => o.GetManifestAsync(It.IsAny<ImageName>(), It.IsAny<bool>()))
            .ReturnsAsync(new ManifestQueryResult("sha256:windows-prior", new JsonObject()));

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

        // Only 2 platforms were built; Windows is missing and will be ported in.
        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfileAmd64, simpleTags: ["tag-amd64"]),
                    CreatePlatform(dockerfileArm64, simpleTags: ["tag-arm64"], architecture: "arm64"))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        await command.ExecuteAsync();

        // Manifest list should reference all 3 platforms (the ported Windows platform
        // joins the two that were built this run).
        dockerServiceMock.Verify(o => o.CreateManifestList(
            "repo:sharedtag",
            It.Is<IEnumerable<string>>(images =>
                images.Contains("repo:tag-amd64") &&
                images.Contains("repo:tag-arm64") &&
                images.Contains("repo:tag-windows")),
            false));
    }

    /// <summary>
    /// Verifies that when a path-filtered official build only rebuilds a subset of an
    /// image's platforms, the shared-tag manifest list is updated to span ALL the
    /// manifest's platforms - by porting the previously-published platform images for
    /// the unrebuilt platforms forward into the scoped staging registry. Without this,
    /// publishing the partial list back to prod would silently regress the previously
    /// complete multi-platform shared tags (see issue #2107).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PortsMissingPlatformsAndPublishesCompleteSharedTagManifestList()
    {
        const string PreviousLinuxDigestSha = "sha256:previous-linux";
        const string NewSharedTagDigestSha = "sha256:new-shared";
        const string SourceRegistry = "source.example.com";
        const string StagingRegistry = "staging.example.com";
        const string StagingRepoPrefix = "build-staging/123456/";

        Mock<IManifestService> manifestServiceMock = CreateManifestServiceMock();
        Mock<IManifestServiceFactory> manifestServiceFactory = CreateManifestServiceFactoryMock(manifestServiceMock);

        // The previously-published Linux platform tag at the source (prod) registry.
        // The porting service queries this to learn the digest to port forward.
        manifestServiceMock
            .Setup(o => o.GetManifestAsync(
                It.Is<ImageName>(n =>
                    n.Registry == SourceRegistry
                    && n.Repo == "samples"
                    && n.Tag == "app-linux"),
                It.IsAny<bool>()))
            .ReturnsAsync(new ManifestQueryResult(PreviousLinuxDigestSha, new JsonObject()));

        // Digest returned for the freshly-published shared tag (used when the command
        // records the new manifest list digest into image-info).
        manifestServiceMock
            .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(NewSharedTagDigestSha);

        Mock<IDockerService> dockerServiceMock = new();
        Mock<ICopyImageService> copyImageServiceMock = new();

        CreateManifestListCommand command = CreateCommand(
            manifestServiceFactory, dockerServiceMock, copyImageServiceMock, Mock.Of<IDateTimeService>());
        command.Options.RegistryOverride = StagingRegistry;
        command.Options.RepoPrefix = StagingRepoPrefix;

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string linuxDockerfile = CreateDockerfile("samples/app", tempFolderContext);
        string windowsDockerfile = "samples/app/Dockerfile.nanoserver";
        CreateFile(windowsDockerfile, tempFolderContext, "FROM base");

        // The manifest declares an image with shared tags ("app", "latest") that span
        // two platforms: Linux and Windows nanoserver. A correctly-published shared-tag
        // manifest list must reference both platforms.
        Manifest manifest = CreateManifest(
            CreateRepo("samples",
                CreateImage(
                    ["app", "latest"],
                    CreatePlatform(linuxDockerfile, ["app-linux"]),
                    CreatePlatform(
                        windowsDockerfile,
                        ["app-nanoserver"],
                        os: OS.Windows,
                        osVersion: "nanoserver-ltsc2022"))));
        manifest.Registry = SourceRegistry;

        // Simulate an incomplete image-info.json - the kind produced by a path-filtered
        // official build where only a subset of an image's platforms actually got built.
        // Here, only the Windows platform is reported as built; the Linux platform from
        // the manifest is absent from this run's image-info.
        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("samples",
                CreateImageData(
                    ["app", "latest"],
                    CreatePlatform(
                        windowsDockerfile,
                        simpleTags: ["app-nanoserver"],
                        osType: "Windows",
                        osVersion: "nanoserver-ltsc2022"))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        await command.ExecuteAsync();

        // The Linux platform should have been imported (by digest) from the source
        // registry into the scoped staging repo at its declared simple tag.
        copyImageServiceMock.Verify(o => o.ImportImageAsync(
            It.Is<string[]>(tags => tags.Contains($"{StagingRepoPrefix}samples:app-linux")),
            StagingRegistry,
            $"samples@{PreviousLinuxDigestSha}",
            true,
            SourceRegistry,
            null,
            false));

        // Each shared tag's new manifest list must reference BOTH the freshly-built
        // Windows platform tag AND the ported Linux platform tag in staging. This keeps
        // "samples:app" / "samples:latest" multi-platform across runs that only rebuild
        // a subset of platforms.
        foreach (string sharedTagName in new[] { "app", "latest" })
        {
            string sharedTagRef = $"{StagingRegistry}/{StagingRepoPrefix}samples:{sharedTagName}";

            dockerServiceMock.Verify(o => o.CreateManifestList(
                sharedTagRef,
                It.Is<IEnumerable<string>>(refs =>
                    refs.Contains($"{StagingRegistry}/{StagingRepoPrefix}samples:app-nanoserver") &&
                    refs.Contains($"{StagingRegistry}/{StagingRepoPrefix}samples:app-linux")),
                false));
            dockerServiceMock.Verify(o => o.PushManifestList(sharedTagRef, false));
        }

        // Ported platforms are temporary scaffolding for manifest-list creation.
        // They should be trimmed before the Publish stage treats image-info as
        // the set of images newly published by this run.
        ImageArtifactDetails updatedImageArtifactDetails =
            ImageArtifactDetails.FromJson(File.ReadAllText(command.Options.ImageInfoPath));
        PlatformData portedPlatform = updatedImageArtifactDetails.Repos
            .Single(repo => repo.Repo == "samples")
            .Images
            .Single()
            .Platforms
            .Single(platform => platform.SimpleTags.Contains("app-linux"));
        portedPlatform.IsUnchanged.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotPortImagesThatAreAbsentFromImageInfo()
    {
        const string SourceRegistry = "source.example.com";
        const string StagingRegistry = "staging.example.com";
        const string StagingRepoPrefix = "build-staging/123456/";

        Mock<IManifestService> manifestServiceMock = new(MockBehavior.Strict);
        manifestServiceMock
            .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("sha256:shared");

        Mock<IDockerService> dockerServiceMock = new();
        Mock<ICopyImageService> copyImageServiceMock = new();

        CreateManifestListCommand command = CreateCommand(
            CreateManifestServiceFactoryMock(manifestServiceMock),
            dockerServiceMock,
            copyImageServiceMock,
            Mock.Of<IDateTimeService>());
        command.Options.RegistryOverride = StagingRegistry;
        command.Options.RepoPrefix = StagingRepoPrefix;

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string builtDockerfile = CreateDockerfile("samples/built", tempFolderContext);
        string untouchedDockerfile = CreateDockerfile("samples/untouched", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("samples",
                CreateImage(
                    ["built"],
                    CreatePlatform(builtDockerfile, ["built-linux"])),
                CreateImage(
                    ["untouched"],
                    CreatePlatform(untouchedDockerfile, ["untouched-linux"]))));
        manifest.Registry = SourceRegistry;

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("samples",
                CreateImageData(
                    ["built"],
                    CreatePlatform(builtDockerfile, simpleTags: ["built-linux"]))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        await command.ExecuteAsync();

        manifestServiceMock.Verify(
            o => o.GetManifestAsync(It.IsAny<ImageName>(), It.IsAny<bool>()),
            Times.Never);
        copyImageServiceMock.Verify(o => o.ImportImageAsync(
                It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<ContainerRegistryImportSourceCredentials>(),
                It.IsAny<bool>()),
            Times.Never);
        dockerServiceMock.Verify(o => o.CreateManifestList(
                $"{StagingRegistry}/{StagingRepoPrefix}samples:untouched",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when a manifest-declared platform is missing from image-info AND
    /// the source registry returns 404 for its tag (e.g. a misconfigured <c>--path</c>
    /// filter excluded a new platform, a manifest tag rename has not been published,
    /// or an MCR mirror lag window), the command fails loudly rather than silently
    /// producing a degraded shared-tag manifest list.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Throws_WhenSourceTagDoesNotExist()
    {
        const string SourceRegistry = "source.example.com";
        const string StagingRegistry = "staging.example.com";
        const string StagingRepoPrefix = "build-staging/123456/";

        Mock<IManifestService> manifestServiceMock = CreateManifestServiceMock();

        HttpRequestException notFound = new(
            "Not Found", inner: null, System.Net.HttpStatusCode.NotFound);
        manifestServiceMock
            .Setup(o => o.GetManifestAsync(It.IsAny<ImageName>(), It.IsAny<bool>()))
            .ThrowsAsync(notFound);
        manifestServiceMock
            .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("sha256:new-shared");

        Mock<IDockerService> dockerServiceMock = new();
        Mock<ICopyImageService> copyImageServiceMock = new();

        Mock<IManifestServiceFactory> manifestServiceFactory =
            CreateManifestServiceFactoryMock(manifestServiceMock);
        CreateManifestListCommand command = CreateCommand(
            manifestServiceFactory, dockerServiceMock, copyImageServiceMock, Mock.Of<IDateTimeService>());
        command.Options.RegistryOverride = StagingRegistry;
        command.Options.RepoPrefix = StagingRepoPrefix;

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string linuxDockerfile = CreateDockerfile("samples/app", tempFolderContext);
        string windowsDockerfile = "samples/app/Dockerfile.nanoserver";
        CreateFile(windowsDockerfile, tempFolderContext, "FROM base");

        Manifest manifest = CreateManifest(
            CreateRepo("samples",
                CreateImage(
                    ["app"],
                    CreatePlatform(linuxDockerfile, ["app-linux"]),
                    CreatePlatform(
                        windowsDockerfile,
                        ["app-nanoserver"],
                        os: OS.Windows,
                        osVersion: "nanoserver-ltsc2022"))));
        manifest.Registry = SourceRegistry;

        // Only Windows in image-info; Linux is missing and its source tag won't be found.
        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("samples",
                CreateImageData(
                    ["app"],
                    CreatePlatform(
                        windowsDockerfile,
                        simpleTags: ["app-nanoserver"],
                        osType: "Windows",
                        osVersion: "nanoserver-ltsc2022"))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        HttpRequestException thrown =
            await Assert.ThrowsAsync<HttpRequestException>(() => command.ExecuteAsync());
        Assert.Equal(System.Net.HttpStatusCode.NotFound, thrown.StatusCode);

        // The 404 fails the command before any imports or manifest-list creation occur.
        copyImageServiceMock.Verify(o => o.ImportImageAsync(
                It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<ContainerRegistryImportSourceCredentials>(),
                It.IsAny<bool>()),
            Times.Never);
        dockerServiceMock.Verify(o => o.CreateManifestList(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when every manifest platform is present in image-info (a full
    /// build, not a path-filtered one), the porting service is a no-op and no
    /// imports are performed.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_DoesNotPort_WhenAllPlatformsArePresent()
    {
        Mock<IManifestService> manifestServiceMock = CreateManifestServiceMock();
        manifestServiceMock
            .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("sha256:new-shared");

        Mock<IDockerService> dockerServiceMock = new();
        Mock<ICopyImageService> copyImageServiceMock = new();

        Mock<IManifestServiceFactory> manifestServiceFactory =
            CreateManifestServiceFactoryMock(manifestServiceMock);
        CreateManifestListCommand command = CreateCommand(
            manifestServiceFactory, dockerServiceMock, copyImageServiceMock, Mock.Of<IDateTimeService>());

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["shared"],
                    CreatePlatform(dockerfile, ["tag1"]))));

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["shared"],
                    CreatePlatform(dockerfile, simpleTags: ["tag1"]))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        await command.ExecuteAsync();

        // No source-registry lookups, no imports.
        manifestServiceMock.Verify(
            o => o.GetManifestAsync(It.IsAny<ImageName>(), It.IsAny<bool>()),
            Times.Never);
        copyImageServiceMock.Verify(o => o.ImportImageAsync(
                It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<ContainerRegistryImportSourceCredentials>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that in dry-run mode the porting service does not call the source
    /// registry and does not import missing platforms.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_DryRun_DoesNotCallSourceRegistry()
    {
        Mock<IManifestService> manifestServiceMock = CreateManifestServiceMock();
        manifestServiceMock
            .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync("sha256:new-shared");

        Mock<IDockerService> dockerServiceMock = new();
        Mock<ICopyImageService> copyImageServiceMock = new();

        Mock<IManifestServiceFactory> manifestServiceFactory =
            CreateManifestServiceFactoryMock(manifestServiceMock);
        CreateManifestListCommand command = CreateCommand(
            manifestServiceFactory, dockerServiceMock, copyImageServiceMock, Mock.Of<IDateTimeService>());
        command.Options.IsDryRun = true;

        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string linuxDockerfile = CreateDockerfile("samples/app", tempFolderContext);
        string windowsDockerfile = "samples/app/Dockerfile.nanoserver";
        CreateFile(windowsDockerfile, tempFolderContext, "FROM base");

        Manifest manifest = CreateManifest(
            CreateRepo("samples",
                CreateImage(
                    ["app"],
                    CreatePlatform(linuxDockerfile, ["app-linux"]),
                    CreatePlatform(
                        windowsDockerfile,
                        ["app-nanoserver"],
                        os: OS.Windows,
                        osVersion: "nanoserver-ltsc2022"))));
        manifest.Registry = "source.example.com";

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("samples",
                CreateImageData(
                    ["app"],
                    CreatePlatform(
                        windowsDockerfile,
                        simpleTags: ["app-nanoserver"],
                        osType: "Windows",
                        osVersion: "nanoserver-ltsc2022"))));

        SetupCommand(command, manifest, imageArtifactDetails, tempFolderContext);

        await command.ExecuteAsync();

        // In dry-run the planner returns an empty plan and the source registry is
        // not queried (it may not be authenticated/reachable in a dry-run context).
        // No imports are performed either, since there's nothing to import.
        manifestServiceMock.Verify(
            o => o.GetManifestAsync(It.IsAny<ImageName>(), It.IsAny<bool>()),
            Times.Never);
        copyImageServiceMock.Verify(o => o.ImportImageAsync(
                It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<ContainerRegistryImportSourceCredentials>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    private static CreateManifestListCommand CreateCommand(
        Mock<IManifestServiceFactory> manifestServiceFactory,
        Mock<IDockerService> dockerServiceMock,
        IDateTimeService dateTimeService) =>
        CreateCommand(manifestServiceFactory, dockerServiceMock, new Mock<ICopyImageService>(), dateTimeService);

    private static CreateManifestListCommand CreateCommand(
        Mock<IManifestServiceFactory> manifestServiceFactory,
        Mock<IDockerService> dockerServiceMock,
        Mock<ICopyImageService> copyImageServiceMock,
        IDateTimeService dateTimeService) =>
        new(
            TestHelper.CreateManifestJsonService(),
            manifestServiceFactory.Object,
            dockerServiceMock.Object,
            copyImageServiceMock.Object,
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
