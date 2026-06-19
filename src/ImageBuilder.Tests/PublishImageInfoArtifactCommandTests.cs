// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class PublishImageInfoArtifactCommandTests
{
    [TestMethod]
    public async Task PublishImageInfoArtifactCommand_PassesImageInfoFileBytesToService()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
        Manifest manifest = CreateManifest(
            CreateRepo("repo1",
                CreateImage(
                    [CreatePlatform(dockerfilePath, ["tag1"])],
                    productVersion: "1.0")));

        string imageInfoContent = JsonHelper.SerializeObject(new ImageArtifactDetails());
        string imageInfoFile = Path.Combine(tempFolderContext.Path, "image-info.json");
        File.WriteAllText(imageInfoFile, imageInfoContent);
        byte[] expectedImageInfoContent = File.ReadAllBytes(imageInfoFile);

        Mock<IImageInfoService> imageInfoServiceMock = new();
        PublishConfiguration publishConfig = new()
        {
            PublishRegistry = new RegistryEndpoint
            {
                Server = "publish.azurecr.io",
                RepoPrefix = "public/"
            }
        };

        PublishImageInfoArtifactCommand command = CreateCommand(imageInfoServiceMock.Object, publishConfig);
        command.Options.ImageInfoPath = imageInfoFile;
        command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
        File.WriteAllText(command.Options.Manifest, JsonConvert.SerializeObject(manifest));

        command.LoadManifest();
        await command.ExecuteAsync();

        imageInfoServiceMock.Verify(
            o => o.PushImageInfoArtifactAsync(
                It.IsAny<ManifestInfo>(),
                It.Is<byte[]>(content => content.SequenceEqual(expectedImageInfoContent)),
                "publish.azurecr.io",
                "public/",
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PublishImageInfoArtifactCommand_NoPublishRegistry_Throws()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string dockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
        Manifest manifest = CreateManifest(
            CreateRepo("repo1",
                CreateImage(
                    [CreatePlatform(dockerfilePath, ["tag1"])],
                    productVersion: "1.0")));

        string imageInfoFile = Path.Combine(tempFolderContext.Path, "image-info.json");
        File.WriteAllText(imageInfoFile, JsonHelper.SerializeObject(new ImageArtifactDetails()));

        Mock<IImageInfoService> imageInfoServiceMock = new();
        PublishImageInfoArtifactCommand command = CreateCommand(imageInfoServiceMock.Object, new PublishConfiguration());
        command.Options.ImageInfoPath = imageInfoFile;
        command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
        File.WriteAllText(command.Options.Manifest, JsonConvert.SerializeObject(manifest));

        command.LoadManifest();

        await Should.ThrowAsync<InvalidOperationException>(command.ExecuteAsync());

        imageInfoServiceMock.Verify(
            o => o.PushImageInfoArtifactAsync(
                It.IsAny<ManifestInfo>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PublishImageInfoArtifactCommand CreateCommand(
        IImageInfoService imageInfoService,
        PublishConfiguration publishConfiguration) =>
        new(
            TestHelper.CreateManifestJsonService(),
            imageInfoService,
            Microsoft.Extensions.Options.Options.Create(publishConfiguration),
            NullLogger<PublishImageInfoArtifactCommand>.Instance);
}
