// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

internal static class ManifestServiceHelper
{
    public record ImageDigestResults(string Image, string Digest);

    public record ImageLayersResults(string Image, IEnumerable<string> Layers);

    public static Mock<IManifestServiceFactory> CreateManifestServiceFactoryMock(
        IEnumerable<ImageDigestResults>? localImageDigestResults = null,
        IEnumerable<ImageDigestResults>? externalImageDigestResults = null,
        IEnumerable<ImageLayersResults>? imageLayersResults = null) =>
            CreateManifestServiceFactoryMock(
                CreateManifestServiceMock(localImageDigestResults, externalImageDigestResults, imageLayersResults));

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
        IEnumerable<ImageDigestResults>? localImageDigestResults = null,
        IEnumerable<ImageDigestResults>? externalImageDigestResults = null,
        IEnumerable<ImageLayersResults>? imageLayersResults = null)
    {
        Mock<IManifestService> manifestServiceMock = new();

        // By default, have it throw an exception which indicates that the manifest was not found
        manifestServiceMock
            .Setup(o => o.GetManifestDigestShaAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception());

        localImageDigestResults ??= [];
        externalImageDigestResults ??= [];
        imageLayersResults ??= [];

        foreach ((string image, string digest) in localImageDigestResults)
        {
            manifestServiceMock
                .Setup(o => o.GetLocalImageDigestAsync(image, false))
                .ReturnsAsync(digest);
        }

        foreach ((string image, string digest) in externalImageDigestResults)
        {
            manifestServiceMock
                .Setup(o => o.GetManifestDigestShaAsync(image, false))
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
