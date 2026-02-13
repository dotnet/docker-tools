// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kusto.Cloud.Platform.Utils;
using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public sealed class GenerateSigningPayloadsCommandTests : IDisposable
{
    private readonly TempFolderContext _tempFolderContext;
    private readonly ImageArtifactDetails _imageInfo;
    private readonly string _payloadOutputDir;

    public GenerateSigningPayloadsCommandTests(ITestOutputHelper outputHelper)
    {
        _tempFolderContext = new TempFolderContext();
        _imageInfo = CreateImageInfo();
        _payloadOutputDir = Path.Combine(_tempFolderContext.Path, "payloads");
    }

    [Fact]
    public async Task GenerateSigningPayloadsCommand_Success()
    {
        string imageInfoPath = Helpers.ImageInfoHelper.WriteImageInfoToDisk(_imageInfo, _tempFolderContext.Path);
        IEnumerable<ImageDigestInfo> imageDigestInfos = GetAllImageDigestInfos(_imageInfo);
        IOrasClient orasClientMock = CreateOrasClientMock(imageDigestInfos);

        GenerateSigningPayloadsCommand command = CreateCommand(imageInfoPath, _payloadOutputDir, orasClientMock);
        await command.ExecuteAsync();

        IEnumerable<string> expectedFilePaths = GetExpectedFilePaths(imageDigestInfos, _payloadOutputDir);
        VerifySigningPayloadsExistOnDisk(expectedFilePaths);
        await VerifyAllPayloadContentsAsync(imageDigestInfos, _payloadOutputDir);
    }

    [Fact]
    public async Task ImagesWithoutManifestLists()
    {
        // Remove all manifest lists from the generated image info.
        RemoveManifestLists(_imageInfo);

        string imageInfoPath = Helpers.ImageInfoHelper.WriteImageInfoToDisk(_imageInfo, _tempFolderContext.Path);
        IEnumerable<ImageDigestInfo> imageDigestInfos = GetAllImageDigestInfos(_imageInfo);
        IOrasClient orasClientMock = CreateOrasClientMock(imageDigestInfos);

        GenerateSigningPayloadsCommand command = CreateCommand(imageInfoPath, _payloadOutputDir, orasClientMock);
        await command.ExecuteAsync();

        IEnumerable<string> expectedFilePaths = GetExpectedFilePaths(imageDigestInfos, _payloadOutputDir);
        VerifySigningPayloadsExistOnDisk(expectedFilePaths);
        await VerifyAllPayloadContentsAsync(imageDigestInfos, _payloadOutputDir);
    }

    public void Dispose()
    {
        _tempFolderContext.Dispose();
    }

    private static async Task VerifyAllPayloadContentsAsync(
        IEnumerable<ImageDigestInfo> imageDigestInfos,
        string payloadOutputDirectory)
    {
        string[] payloadFilePaths = Directory.GetFiles(payloadOutputDirectory);

        // Sort for comparison
        ImageDigestInfo[] orderedImageDigestInfos = imageDigestInfos.OrderBy(d => TrimDigest(d.Digest)).ToArray();
        string[] orderedPayloadFilePaths = payloadFilePaths.OrderBy(path => Path.GetFileName(path)).ToArray();

        Assert.Equal(orderedImageDigestInfos.Length, orderedPayloadFilePaths.Length);

        // Compare expected and actual results pairwise
        IEnumerable<(ImageDigestInfo DigestInfo, string Path)> pairs = 
            orderedImageDigestInfos.Zip(orderedPayloadFilePaths);
        await Parallel.ForEachAsync(pairs, (p, _) => ValidatePayloadContentsAsync(p.DigestInfo, p.Path));
    }

    private static async ValueTask ValidatePayloadContentsAsync(ImageDigestInfo digestInfo, string payloadFilePath)
    {
        string payloadJson = await File.ReadAllTextAsync(payloadFilePath);
        Descriptor targetArtifact = Payload.FromJson(payloadJson).TargetArtifact;
        Assert.Equal(TrimDigest(digestInfo.Digest), TrimDigest(targetArtifact.Digest));
        Assert.Equal(digestInfo.IsManifestList, targetArtifact.MediaType.Contains("manifest.list"));
    }

    private static void VerifySigningPayloadsExistOnDisk(IEnumerable<string> expectedFilePaths)
    {
        foreach (string filePath in expectedFilePaths)
        {
            Assert.True(File.Exists(filePath), $"Payload file '{filePath}' does not exist");
        }
    }

    private static IEnumerable<string> GetExpectedFilePaths(
        IEnumerable<ImageDigestInfo> imageDigestInfos,
        string payloadOutputDir)
    {
        return imageDigestInfos
            .Select(digestInfo => {
                    string fileName = TrimDigest(digestInfo.Digest) + ".json";
                    return Path.Combine(payloadOutputDir, fileName);
                })
            .Distinct();
    }

    private static GenerateSigningPayloadsCommand CreateCommand(
        string imageInfoPath,
        string outputDir,
        IOrasClient orasClient)
    {
        GenerateSigningPayloadsCommand command = new(
            Mock.Of<ILogger>(),
            orasClient,
            Mock.Of<IRegistryCredentialsProvider>());
        command.Options.ImageInfoPath = imageInfoPath;
        command.Options.PayloadOutputDirectory = outputDir;
        return command;
    }

    /// <summary>
    /// Assuming all images have been pushed to the registry, create an ORAS client mock which returns the OCI
    /// descriptor for an image when querying via digest or tags.
    /// </summary>
    private static IOrasClient CreateOrasClientMock(IEnumerable<ImageDigestInfo> imageDigestInfos)
    {
        Mock<IOrasClient> orasClientMock = new();
        foreach (ImageDigestInfo digestInfo in imageDigestInfos)
        {
            SetupMockDescriptors(orasClientMock, digestInfo);
        }

        return orasClientMock.Object;
    }

    private static Mock<IOrasClient> SetupMockDescriptors(Mock<IOrasClient> orasClientMock, ImageDigestInfo digestInfo)
    {
        Descriptor descriptor = CreateDescriptor(digestInfo.Digest, digestInfo.IsManifestList);

        orasClientMock
            .Setup(o => o.GetDescriptor(digestInfo.Digest, false))
            .Returns(descriptor);
        
        foreach (string tag in digestInfo.Tags)
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
        string registry = "myregistry.azurecr.io";
        string[] repos = [ "runtime-deps", "runtime" ];
        string[] oses = [ "foolinux", "barlinux" ];
        string[] archs = [ "amd64", "arm64v8", "arm32v7" ];
        string[] versions = [ "2.0", "99.0" ];

        return Helpers.ImageInfoHelper.CreateImageInfo(registry, repos, oses, archs, versions);
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

    private static IReadOnlyList<ImageDigestInfo> GetAllImageDigestInfos(ImageArtifactDetails imageInfo)
    {
        IEnumerable<ImageData> allImages = imageInfo.Repos.SelectMany(repo => repo.Images);

        IEnumerable<ImageDigestInfo> platformDigestsAndTags =
            allImages
                .SelectMany(image => image.Platforms)
                .Select(platform =>
                    new ImageDigestInfo(
                        Digest: platform.Digest,
                        Tags: platform.SimpleTags,
                        IsManifestList: false));

        IEnumerable<ImageDigestInfo> manifestListDigestsAndTags = 
            allImages
                .Select(image => image.Manifest)
                .Where(manifest => manifest is not null)
                .Select(manifestList =>
                    new ImageDigestInfo(
                        Digest: manifestList.Digest,
                        Tags: manifestList.SharedTags,
                        IsManifestList: true));

        return [..platformDigestsAndTags, ..manifestListDigestsAndTags];
    }

    private static string TrimDigest(string fullDigest) => fullDigest.Split(':')[1];
}
