// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Commands.Signing;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class VerifySignaturesOptionsBindingTests
{
    [Fact]
    public void RegistryOverride_BoundFromCliArgs()
    {
        string[] args =
        [
            "image-info.json",
            "--registry-override", "myregistry.azurecr.io",
        ];

        VerifySignaturesOptions options = ParseAndBind<VerifySignaturesOptions>(args);

        options.RegistryOverride.Registry.ShouldBe("myregistry.azurecr.io");
    }

    [Fact]
    public void RepoPrefix_BoundFromCliArgs()
    {
        string[] args =
        [
            "image-info.json",
            "--repo-prefix", "public/",
        ];

        VerifySignaturesOptions options = ParseAndBind<VerifySignaturesOptions>(args);

        options.RegistryOverride.RepoPrefix.ShouldBe("public/");
    }

    private static TOptions ParseAndBind<TOptions>(string[] args)
        where TOptions : Options, new()
    {
        TOptions options = new();
        Command command = new("test", "test");
        command.AddOptions(options);

        ParseResult parseResult = command.Parse(args);
        options.Bind(parseResult);
        return options;
    }
}
