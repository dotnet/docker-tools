// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Newtonsoft.Json;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class ImageCacheServiceTests
{
    [TestMethod]
    [DataRow("match", CacheMissReason.None)]
    [DataRow("missing-info", CacheMissReason.NoPreviousImageInfo)]
    [DataRow("dockerfile", CacheMissReason.DockerfileChanged)]
    [DataRow("digest", CacheMissReason.BaseImageDigestChanged)]
    [DataRow("registry", CacheMissReason.RegistryImageUnavailable)]
    [DataRow("local", CacheMissReason.LocalImageDigestUnavailable)]
    [DataRow("missing-digest", CacheMissReason.NoPreviousBaseImageDigest)]
    [DataRow("scratch", CacheMissReason.None)]
    [DataRow("missing-tags", CacheMissReason.None)]
    public async Task CheckForCachedImage_ReturnsReason(string scenario, CacheMissReason expectedReason)
    {
        using Fixture fixture = new(scenario);
        ImageCacheResult result = await fixture.CheckAsync(scenario == "missing-info" ? null : fixture.Source);

        result.MissReason.ShouldBe(expectedReason);
        result.State.ShouldBe(expectedReason != CacheMissReason.None
            ? ImageCacheState.NotCached
            : scenario == "missing-tags" ? ImageCacheState.CachedWithMissingTags : ImageCacheState.Cached);
        result.IsNewCacheHit.ShouldBe(expectedReason == CacheMissReason.None);
        fixture.Service.HasAnyCachedPlatforms.ShouldBe(expectedReason == CacheMissReason.None);
    }

    [TestMethod]
    public async Task CheckForCachedImage_ReusesMemoryCacheWithoutPreviousImageInfo()
    {
        using Fixture fixture = new("match");
        ImageCacheResult first = await fixture.CheckAsync(fixture.Source);
        first.State.ShouldBe(ImageCacheState.Cached);

        ImageCacheResult second = await fixture.CheckAsync(null);
        second.State.ShouldBe(ImageCacheState.CachedWithMissingTags);
        second.MissReason.ShouldBe(CacheMissReason.None);
        second.IsNewCacheHit.ShouldBeFalse();
        second.Platform.ShouldBeSameAs(first.Platform);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task CheckForCachedImage_PropagatesRegistryErrors(HttpStatusCode status)
    {
        using Fixture fixture = new("match");
        HttpRequestException error = new("Registry request failed", null, status);
        fixture.Registry.Setup(service => service.GetManifestDigestShaAsync(
            It.IsAny<string>(), false, It.IsAny<CancellationToken>())).ThrowsAsync(error);

        HttpRequestException actual = await Should.ThrowAsync<HttpRequestException>(
            () => fixture.CheckAsync(fixture.Source));
        actual.ShouldBeSameAs(error);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("image-cache-tests-").FullName;
        private readonly PlatformData _platform;
        private readonly ImageDigestCache _digests;
        private readonly ImageNameResolver _resolver;
        private readonly bool _local;
        public Mock<IManifestService> Registry { get; } = new();
        public ImageCacheService Service { get; }
        public ImageData Source { get; }

        public Fixture(string scenario)
        {
            File.WriteAllText(Path.Combine(_directory, "Dockerfile"),
                scenario == "scratch" ? "FROM scratch\n" : "FROM example.com/base:latest\n");
            Manifest model = ManifestHelper.CreateManifest(ManifestHelper.CreateRepo("test",
                ManifestHelper.CreateImage(ManifestHelper.CreatePlatform("Dockerfile", ["latest"]))));
            model.Registry = "example.com";
            string manifestPath = Path.Combine(_directory, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(model));
            ManifestInfo manifest = TestHelper.CreateManifestJsonService().Load(
                new BuildOptions { Manifest = manifestPath });
            PlatformInfo platform = manifest.GetAllPlatforms().Single();
            _platform = new PlatformData { PlatformInfo = platform };
            Source = new ImageData
            {
                Platforms = [new PlatformData
                {
                    PlatformInfo = platform,
                    BaseImageDigest = scenario == "missing-digest" ? null : "sha256:old",
                    CommitUrl = scenario == "dockerfile" ? "previous" : "https://example.com/repo/blob/current/Dockerfile",
                    SimpleTags = scenario == "missing-tags" ? [] : ["latest"]
                }]
            };
            Mock<IGitService> git = new();
            git.Setup(service => service.GetCommitSha(platform.DockerfilePath, true)).Returns("current");
            Service = new ImageCacheService(Mock.Of<ILogger<ImageCacheService>>(), git.Object);
            Registry.Setup(service => service.GetManifestDigestShaAsync(
                It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(scenario == "digest" ? "sha256:new" : "sha256:old");
            if (scenario == "registry")
            {
                Registry.Setup(service => service.GetManifestDigestShaAsync(
                    It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("Not found", null, HttpStatusCode.NotFound));
            }
            _local = scenario == "local";
            Registry.Setup(service => service.GetLocalImageDigestAsync(
                It.IsAny<ImageName>(), false, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
            _digests = new ImageDigestCache(new Lazy<IManifestService>(() => Registry.Object));
            _resolver = new ImageNameResolverForBuild(new BaseImageOverrideOptions(),
                manifest, null, null);
        }

        public Task<ImageCacheResult> CheckAsync(ImageData? source) => Service.CheckForCachedImageAsync(
            source, _platform, _digests, _resolver, "https://example.com/repo", _local, false, CancellationToken.None);

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
