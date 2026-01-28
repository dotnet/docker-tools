// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

internal static class ConfigurationHelper
{
    /// <summary>
    /// Sets up a mock of IOptions that returns <paramref name="value"/>.
    /// </summary>
    /// <param name="value">
    /// The value to be returned by the IOptions mock.
    /// If <paramref name="value"/> is null, a new instance of T is created.
    /// </param>
    public static IOptions<T> CreateOptionsMock<T>(T? value = null) where T : class, new()
    {
        var optionsMock = new Mock<IOptions<T>>();
        var config = value ?? new T();
        optionsMock.Setup(o => o.Value).Returns(config);
        return optionsMock.Object;
    }
}
