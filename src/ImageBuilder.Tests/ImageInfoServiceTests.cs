// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;
using OrasManifest = OrasProject.Oras.Oci.Manifest;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class ImageInfoServiceTests
{
    private static readonly byte[] ImageInfoContent = [];

    [TestMethod]
    public async Task PushImageInfoArtifactAsync_PushesToGivenRegistryWithRepoPrefixAndManifestTag()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);
        ManifestInfo manifest = CreateManifestInfo(tempFolderContext, dockerfile);

        Mock<IOrasService> orasServiceMock = new();
        ImageInfoService service = CreateService(orasServiceMock.Object);

        await service.PushImageInfoArtifactAsync(
            manifest,
            ImageInfoContent,
            registry: "publish.example.com",
            repoPrefix: "public/",
            isDryRun: false);

        orasServiceMock.Verify(o => o.PushArtifactAsync(
            ImageInfoContent,
            OciArtifactType.ImageInfo,
            OciArtifactType.ImageInfo,
            "publish.example.com",
            "public/dotnet/versions",
            It.Is<IEnumerable<string>>(tags => tags.SequenceEqual(new[] { "latest" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task PushImageInfoArtifactAsync_MultipleTags_PushesOneArtifactWithAllTags()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);
        ImageInfoArtifact imageInfoArtifact = new()
        {
            Repo = "dotnet/versions",
            Tags = new Dictionary<string, Tag>
            {
                ["latest"] = new(),
                ["main"] = new(),
            }
        };
        ManifestInfo manifest = CreateManifestInfo(tempFolderContext, dockerfile, imageInfoArtifact);

        Mock<IOrasService> orasServiceMock = new();
        ImageInfoService service = CreateService(orasServiceMock.Object);

        await service.PushImageInfoArtifactAsync(
            manifest,
            ImageInfoContent,
            registry: "publish.example.com",
            repoPrefix: null,
            isDryRun: false);

        orasServiceMock.Verify(o => o.PushArtifactAsync(
            ImageInfoContent,
            OciArtifactType.ImageInfo,
            OciArtifactType.ImageInfo,
            "publish.example.com",
            "dotnet/versions",
            It.Is<IEnumerable<string>>(tags =>
                tags.OrderBy(t => t).SequenceEqual(new[] { "latest", "main" })),
            It.IsAny<CancellationToken>()), Times.Once);
        orasServiceMock.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task PushImageInfoArtifactAsync_DryRun_DoesNotPush()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);
        ManifestInfo manifest = CreateManifestInfo(tempFolderContext, dockerfile);

        Mock<IOrasService> orasServiceMock = new();
        ImageInfoService service = CreateService(orasServiceMock.Object);

        await service.PushImageInfoArtifactAsync(
            manifest,
            ImageInfoContent,
            registry: "publish.example.com",
            repoPrefix: "public/",
            isDryRun: true);

        orasServiceMock.Verify(o => o.PushArtifactAsync(
            It.IsAny<byte[]>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task PushImageInfoArtifactAsync_NoManifestImageInfo_Throws()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);
        ManifestInfo manifest = CreateManifestInfo(
            tempFolderContext,
            dockerfile,
            includeImageInfoArtifact: false);

        Mock<IOrasService> orasServiceMock = new();
        ImageInfoService service = CreateService(orasServiceMock.Object);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.PushImageInfoArtifactAsync(
                manifest,
                ImageInfoContent,
                registry: "publish.example.com",
                repoPrefix: "public/",
                isDryRun: false));

        orasServiceMock.Verify(o => o.PushArtifactAsync(
            It.IsAny<byte[]>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task PullImageInfoArtifactAsync_PullsConfiguredArtifact()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);
        ManifestInfo manifest = CreateManifestInfo(tempFolderContext, dockerfile);
        ImageArtifactDetails imageInfo = new()
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "repo"
                }
            ]
        };

        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.PullAsync(
                "publish.example.com",
                "public/dotnet/versions",
                "latest",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OciArtifact(
                CreateManifestDescriptor(),
                CreateOrasManifest(OciArtifactType.ImageInfo, OciArtifactType.ImageInfo),
                [CreateBlob(Encoding.UTF8.GetBytes(JsonHelper.SerializeObject(imageInfo)), OciArtifactType.ImageInfo)]));
        ImageInfoService service = CreateService(orasServiceMock.Object);

        string result = await service.PullImageInfoArtifactAsync(
            manifest,
            registry: "publish.example.com",
            repoPrefix: "public/");

        result.ShouldBe(JsonHelper.SerializeObject(imageInfo));
        orasServiceMock.VerifyAll();
    }

    [TestMethod]
    public async Task PullImageInfoArtifactAsync_WrongArtifactType_Throws()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);
        ManifestInfo manifest = CreateManifestInfo(tempFolderContext, dockerfile);

        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.PullAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OciArtifact(
                CreateManifestDescriptor(),
                CreateOrasManifest("application/vnd.other", OciArtifactType.ImageInfo),
                [CreateBlob(Encoding.UTF8.GetBytes("{}"), OciArtifactType.ImageInfo)]));
        ImageInfoService service = CreateService(orasServiceMock.Object);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.PullImageInfoArtifactAsync(
                manifest,
                registry: "publish.example.com",
                repoPrefix: "public/"));

        exception.Message.ShouldContain("artifactType 'application/vnd.other'");
    }

    [TestMethod]
    public async Task PullImageInfoArtifactAsync_WrongMediaType_Throws()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);
        ManifestInfo manifest = CreateManifestInfo(tempFolderContext, dockerfile);

        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.PullAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OciArtifact(
                CreateManifestDescriptor(),
                CreateOrasManifest(OciArtifactType.ImageInfo, "application/vnd.other"),
                [CreateBlob(Encoding.UTF8.GetBytes("{}"), "application/vnd.other")]));
        ImageInfoService service = CreateService(orasServiceMock.Object);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.PullImageInfoArtifactAsync(
                manifest,
                registry: "publish.example.com",
                repoPrefix: "public/"));

        exception.Message.ShouldContain("mediaType 'application/vnd.other'");
    }

    [TestMethod]
    public async Task PullImageInfoArtifactAsync_MultipleLayers_Throws()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);
        ManifestInfo manifest = CreateManifestInfo(tempFolderContext, dockerfile);

        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.PullAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OciArtifact(
                CreateManifestDescriptor(),
                CreateOrasManifest(
                    OciArtifactType.ImageInfo,
                    OciArtifactType.ImageInfo,
                    OciArtifactType.ImageInfo),
                [
                    CreateBlob(Encoding.UTF8.GetBytes("{}"), OciArtifactType.ImageInfo),
                    CreateBlob(Encoding.UTF8.GetBytes("{}"), OciArtifactType.ImageInfo)
                ]));
        ImageInfoService service = CreateService(orasServiceMock.Object);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.PullImageInfoArtifactAsync(
                manifest,
                registry: "publish.example.com",
                repoPrefix: "public/"));

        exception.Message.ShouldContain("must have exactly one blob, but has 2");
    }

    private static ImageInfoService CreateService(IOrasService orasService)
    {
        Mock<IOrasServiceFactory> orasServiceFactoryMock = new();
        orasServiceFactoryMock
            .Setup(f => f.Create(It.IsAny<IRegistryCredentialsHost?>()))
            .Returns(orasService);
        return new ImageInfoService(
            TestHelper.CreateManifestJsonService(),
            orasServiceFactoryMock.Object,
            NullLogger<ImageInfoService>.Instance);
    }

    private static ManifestInfo CreateManifestInfo(
        TempFolderContext tempFolderContext,
        string dockerfile,
        ImageInfoArtifact? imageInfoArtifact = null,
        bool includeImageInfoArtifact = true)
    {
        string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    CreatePlatform(dockerfile, ["tag"]))));
        manifest.Registry = "public.example.com";
        if (includeImageInfoArtifact)
        {
            manifest.ImageInfo = imageInfoArtifact ?? new ImageInfoArtifact
            {
                Repo = "dotnet/versions",
                Tags = new Dictionary<string, Tag>
                {
                    ["latest"] = new()
                }
            };
        }

        File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));

        Mock<IManifestOptionsInfo> manifestOptionsMock = new();
        manifestOptionsMock
            .SetupGet(o => o.Manifest)
            .Returns(manifestPath);
        manifestOptionsMock
            .Setup(o => o.GetManifestFilter())
            .Returns(new ManifestFilter([]));

        return TestHelper.CreateManifestJsonService().Load(manifestOptionsMock.Object);
    }

    private static OrasDescriptor CreateManifestDescriptor() => new()
    {
        MediaType = "application/vnd.oci.image.manifest.v1+json",
        Digest = "sha256:manifest",
        Size = 1
    };

    private static OrasManifest CreateOrasManifest(string artifactType, params string[] layerMediaTypes)
    {
        return new OrasManifest
        {
            ArtifactType = artifactType,
            Config = OrasDescriptor.Empty,
            Layers = layerMediaTypes
                .Select(CreateLayerDescriptor)
                .ToList(),
            SchemaVersion = 2
        };
    }

    private static OciBlob CreateBlob(byte[] content, string mediaType) =>
        new(content, CreateLayerDescriptor(mediaType));

    private static OrasDescriptor CreateLayerDescriptor(string mediaType) => new()
    {
        MediaType = mediaType,
        Digest = "sha256:layer",
        Size = 1
    };
}
