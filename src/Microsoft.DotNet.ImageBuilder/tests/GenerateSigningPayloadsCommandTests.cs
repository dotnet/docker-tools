// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kusto.Cloud.Platform.Utils;
using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Tests;

using ImageDigestInfo = (string Digest, List<string> Tags);

public sealed class GenerateSigningPayloadsCommandTests : IDisposable
{
    private readonly ITestOutputHelper _outputHelper;

    // Test Context
    private readonly TempFolderContext _tempFolderContext;
    private readonly ImageArtifactDetails _imageInfo;
    private readonly string _payloadOutputDir;

    public GenerateSigningPayloadsCommandTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;

        // Shared test initialization
        _tempFolderContext = new TempFolderContext();
        _imageInfo = CreateImageInfo();
        _payloadOutputDir = Path.Combine(_tempFolderContext.Path, "payloads");
    }

    [Fact]
    public async Task GenerateSigningPayloadsCommand_Success()
    {
        string imageInfoPath = Helpers.ImageInfoHelper.WriteImageInfoToDisk(_imageInfo, _tempFolderContext.Path);
        IOrasClient orasClientMock = CreateOrasClientMock(_imageInfo);

        GenerateSigningPayloadsCommand command = CreateCommand(imageInfoPath, _payloadOutputDir, orasClientMock);
        await command.ExecuteAsync();

        VerifySigningPayloadsExistOnDisk(_imageInfo, _payloadOutputDir);
    }

    [Fact]
    public async Task ImagesWithoutManifestLists()
    {
        // Remove all manifest lists from the generated image info.
        RemoveManifestLists(_imageInfo);

        string imageInfoPath = Helpers.ImageInfoHelper.WriteImageInfoToDisk(_imageInfo, _tempFolderContext.Path);
        IOrasClient orasClientMock = CreateOrasClientMock(_imageInfo);

        GenerateSigningPayloadsCommand command = CreateCommand(imageInfoPath, _payloadOutputDir, orasClientMock);
        await command.ExecuteAsync();

        VerifySigningPayloadsExistOnDisk(_imageInfo, _payloadOutputDir);
    }

    public void Dispose()
    {
        _tempFolderContext.Dispose();
    }

    private static void VerifySigningPayloadsExistOnDisk(ImageArtifactDetails imageInfo, string payloadOutputDir)
    {
        IEnumerable<string> expectedFilePaths = ImageInfoHelper.GetAllDigests(imageInfo)
            .Select(DockerHelper.TrimDigestAlgorithm)
            .Select(digest => Path.Combine(payloadOutputDir, digest + ".json"));

        foreach (string filePath in expectedFilePaths)
        {
            Assert.True(File.Exists(filePath), $"Payload file '{filePath}' does not exist");
        }
    }

    private static GenerateSigningPayloadsCommand CreateCommand(
        string imageInfoPath,
        string outputDir,
        IOrasClient orasClient)
    {
        GenerateSigningPayloadsCommand command = new(Mock.Of<ILoggerService>(), orasClient);
        command.Options.ImageInfoPath = imageInfoPath;
        command.Options.PayloadOutputDirectory = outputDir;
        return command;
    }

    /// <summary>
    /// Assuming all images have been pushed to the registry, create an ORAS client mock which returns the OCI
    /// descriptor for an image when querying via digest or tags.
    /// </summary>
    private static IOrasClient CreateOrasClientMock(ImageArtifactDetails imageArtifactDetails)
    {
        Mock<IOrasClient> orasClientMock = new();

        IEnumerable<ImageDigestInfo> platformDigestsAndTags =
            imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(image => image.Platforms)
                .Select(platform => (platform.Digest, Tags: platform.SimpleTags));

        IEnumerable<ImageDigestInfo> manifestListDigestsAndTags = 
            imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images)
                .Select(image => image.Manifest)
                .Where(manifest => manifest is not null)
                .Select(manifest => (manifest.Digest, Tags: manifest.SharedTags));

        foreach (ImageDigestInfo imageDigest in platformDigestsAndTags)
        {
            SetupMockDescriptors(orasClientMock, imageDigest, isManifestList: false);
        }

        foreach (ImageDigestInfo imageDigest in manifestListDigestsAndTags)
        {
            SetupMockDescriptors(orasClientMock, imageDigest, isManifestList: true);
        }

        return orasClientMock.Object;
    }

    private static Mock<IOrasClient> SetupMockDescriptors(Mock<IOrasClient> orasClientMock, ImageDigestInfo image, bool isManifestList)
    {
        Descriptor descriptor = CreateDescriptor(image.Digest, isManifestList);

        orasClientMock
            .Setup(o => o.GetDescriptor(image.Digest, false))
            .Returns(descriptor);
        
        foreach (string tag in image.Tags)
        {
            orasClientMock
                .Setup(o => o.GetDescriptor(tag, false))
                .Returns(descriptor);
        }

        return orasClientMock;
    }

    private static Descriptor CreateDescriptor(string digest, bool isManifestList)
    {
        string mediaType = isManifestList
            ? "application/vnd.docker.distribution.manifest.list.v2+json"
            : "application/vnd.docker.distribution.manifest.v2+json";

        return new Descriptor(MediaType: mediaType, Digest: digest, Size: 999);
    }

    /// <summary>
    /// Create some generic test image info spanning multiple repos, versions, OSes, and architectures.
    /// </summary>
    private static ImageArtifactDetails CreateImageInfo()
    {
        string[] repos = [ "runtime-deps", "runtime" ];
        string[] oses = [ "foolinux", "barlinux" ];
        string[] archs = [ "amd64", "arm64v8", "arm32v7" ];
        string[] versions = [ "2.0", "99.0" ];

        return Helpers.ImageInfoHelper.CreateImageInfo(repos, oses, archs, versions);
    }

    /// <summary>
    /// Removes manifest lists from image info for some tests.
    /// </summary>
    private static void RemoveManifestLists(ImageArtifactDetails imageInfo)
    {
        imageInfo.Repos
            .SelectMany(repo => repo.Images)
            .ForEach(image => image.Manifest = null);
    }
}
