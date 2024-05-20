using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

internal static class ManifestServiceHelper
{
    public record ImageDigestResults(string Image, string Digest);

    public record ImageLayersResults(string Image, IEnumerable<string> Layers);

    public static Mock<IManifestServiceFactory> CreateManifestServiceFactoryMock(
        IEnumerable<ImageDigestResults>? imageDigestResults = null,
        IEnumerable<ImageLayersResults>? imageLayersResults = null) =>
            CreateManifestServiceFactoryMock(CreateManifestServiceMock(imageDigestResults, imageLayersResults));

    public static Mock<IManifestServiceFactory> CreateManifestServiceFactoryMock(
        Mock<IInnerManifestService> innerManifestService) =>
            CreateManifestServiceFactoryMock(new ManifestService(innerManifestService.Object));

    public static Mock<IManifestServiceFactory> CreateManifestServiceFactoryMock(
        Mock<IManifestService> manifestServiceMock) =>
            CreateManifestServiceFactoryMock(manifestServiceMock.Object);

    public static Mock<IManifestServiceFactory> CreateManifestServiceFactoryMock(
        IManifestService manifestService)
    {
        Mock<IManifestServiceFactory> manifestServiceFactoryMock = new();
        manifestServiceFactoryMock
            .Setup(o => o.Create(It.IsAny<string?>(), It.IsAny<IRegistryCredentialsHost>()))
            .Returns(manifestService);

        return manifestServiceFactoryMock;
    }

    public static Mock<IManifestService> CreateManifestServiceMock(
        IEnumerable<ImageDigestResults>? imageDigestResults = null,
        IEnumerable<ImageLayersResults>? imageLayersResults = null)
    {
        Mock<IManifestService> manifestServiceMock = new();

        imageDigestResults ??= [];
        imageLayersResults ??= [];

        foreach ((string image, string digest) in imageDigestResults)
        {
            manifestServiceMock
                .Setup(o => o.GetImageDigestAsync(image, false))
                .ReturnsAsync(digest);
        }

        foreach ((string image, IEnumerable<string> layers) in imageLayersResults)
        {
            manifestServiceMock
                .Setup(o => o.GetImageLayersAsync(image, false))
                .ReturnsAsync(layers);
        }

        return manifestServiceMock;
    }
}
