// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Moq;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

internal static class CopyImageHelper
{
    /// <summary>
    /// Creates a mock of the <see cref="ICopyImageServiceFactory"/> interface.
    /// </summary>
    /// <param name="copyImageService">Optional service to return when calling the factory's Create method.</param>
    /// <returns></returns>
    public static Mock<ICopyImageServiceFactory> CreateCopyImageServiceFactoryMock(
        ICopyImageService? copyImageService = null)
    {
        copyImageService ??= new Mock<ICopyImageService>().Object;

        var factoryMock = new Mock<ICopyImageServiceFactory>();
        factoryMock
            .Setup(o => o.Create(It.IsAny<IServiceConnection>()))
            .Returns(copyImageService);

        return factoryMock;
    }
}
