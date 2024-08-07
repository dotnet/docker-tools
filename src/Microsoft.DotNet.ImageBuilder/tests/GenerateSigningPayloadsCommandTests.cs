// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.NET.StringTools;
using Moq;
using Octokit;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Tests;

using ImageDigestInfo = (string Digest, List<string> Tags);

public class GenerateSigningPayloadsCommandTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly DateOnly _globalDate = new DateOnly(2024, 6, 10);
    private readonly DateOnly _specificDigestDate = new DateOnly(2022, 1, 1);
    private const string RepoPrefix = "public/";
    private const string AcrName = "myacr.azurecr.io";
    private const string AnnotationsOutputPath = "annotations.txt";
    private const string AnnotationDigest1 = "annotationdigest1";
    private const string AnnotationDigest2 = "annotationdigest2";

    public GenerateSigningPayloadsCommandTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task GenerateSigningPayloadsCommand_Success()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
        string payloadOutputDir = Path.Combine(tempFolderContext.Path, "payloads");

        string[] repos = [ "runtime-deps", "runtime" ];
        string[] oses = [ "foolinux", "barlinux" ];
        string[] archs = [ "amd64", "arm64v8", "arm32v7" ];
        string[] versions = [ "8.0", "10.0" ];

        (ImageArtifactDetails imageInfo, string imageInfoPath) =
            Helpers.ImageInfoHelper.CreateImageInfoOnDisk(tempFolderContext, repos, oses, archs, versions);

        IOrasClient orasClientMock = CreateOrasClientMock(imageInfo);

        GenerateSigningPayloadsCommand command = CreateCommand(imageInfoPath, payloadOutputDir, orasClientMock);

        await command.ExecuteAsync();

        Assert.Equal(true, true);
    }

    private static GenerateSigningPayloadsCommand CreateCommand(string imageInfoPath, string outputDir, IOrasClient orasClient)
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
}
