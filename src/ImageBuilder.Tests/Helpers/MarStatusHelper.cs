// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Moq;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

internal static class MarStatusHelper
{
    /// <summary>
    /// Creates a mock of the <see cref="IMcrStatusClientFactory"/> interface.
    /// </summary>
    /// <param name="client">Optional client to return when calling the factory's Create method.</param>
    /// <returns>The <see cref="IMcrStatusClientFactory"/> mock.</returns>
    public static Mock<IMcrStatusClientFactory> CreateMarStatusClientFactoryMock(
        IMcrStatusClient? client = null)
    {
        client ??= new Mock<IMcrStatusClient>().Object;

        var factoryMock = new Mock<IMcrStatusClientFactory>();
        factoryMock
            .Setup(o => o.Create(It.IsAny<IServiceConnection>()))
            .Returns(client);

        return factoryMock;
    }
}
