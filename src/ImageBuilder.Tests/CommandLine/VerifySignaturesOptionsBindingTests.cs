// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.CommandLine.OptionsBindingTestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.CommandLine;

[TestClass]
public class VerifySignaturesOptionsBindingTests
{
    [TestMethod]
    public void RegistryOverride_BoundFromCliArgs()
    {
        string[] args = ["image-info.json", "--registry-override", "myregistry.azurecr.io"];
        VerifySignaturesOptions options = ParseAndBind<VerifySignaturesOptions>(args);
        options.RegistryOverride.Registry.ShouldBe("myregistry.azurecr.io");
    }

    [TestMethod]
    public void RepoPrefix_BoundFromCliArgs()
    {
        string[] args = ["image-info.json", "--repo-prefix", "public/"];
        VerifySignaturesOptions options = ParseAndBind<VerifySignaturesOptions>(args);
        options.RegistryOverride.RepoPrefix.ShouldBe("public/");
    }
}
