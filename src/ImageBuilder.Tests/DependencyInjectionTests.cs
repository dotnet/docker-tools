// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void DependencyResolution()
    {
        var commands = ImageBuilder.Commands.ToArray();
        Assert.NotNull(commands);
        Assert.NotEmpty(commands);
    }
}
