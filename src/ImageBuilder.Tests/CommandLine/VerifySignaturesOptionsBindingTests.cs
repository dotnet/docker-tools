// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using static Microsoft.DotNet.ImageBuilder.Tests.CommandLine.OptionsBindingTestHelper;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.CommandLine;

public class VerifySignaturesOptionsBindingTests
{
    [Fact]
    public void RegistryOverride_BoundFromCliArgs()
    {
        string[] args = ["image-info.json", "--registry-override", "myregistry.azurecr.io"];
        VerifySignaturesOptions options = ParseAndBind<VerifySignaturesOptions>(args);
        options.RegistryOverride.Registry.ShouldBe("myregistry.azurecr.io");
    }

    [Fact]
    public void RepoPrefix_BoundFromCliArgs()
    {
        string[] args = ["image-info.json", "--repo-prefix", "public/"];
        VerifySignaturesOptions options = ParseAndBind<VerifySignaturesOptions>(args);
        options.RegistryOverride.RepoPrefix.ShouldBe("public/");
    }
}
