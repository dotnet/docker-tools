// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class DependencyInjectionTests
{
    [TestMethod]
    public void DependencyResolution()
    {
        using IHost host = ImageBuilder.CreateAppHost();

        ICommand[] commands = host.Services.GetServices<ICommand>().ToArray();

        commands.ShouldNotBeNull();
        commands.ShouldNotBeEmpty();
    }
}
