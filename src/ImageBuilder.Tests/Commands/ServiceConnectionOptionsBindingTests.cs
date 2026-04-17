// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ImageBuilder.Commands;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Commands;

public class ServiceConnectionOptionsBindingTests
{
    [Fact]
    public void ValidFormat_ParsesCorrectly()
    {
        string[] args =
        [
            "--storage-service-connection", "my-tenant:my-client:my-connection-id",
        ];

        BuildOptions options = ParseAndBind<BuildOptions>(args);

        options.StorageServiceConnection.ShouldNotBeNull();
        options.StorageServiceConnection.TenantId.ShouldBe("my-tenant");
        options.StorageServiceConnection.ClientId.ShouldBe("my-client");
        options.StorageServiceConnection.Id.ShouldBe("my-connection-id");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("only:two")]
    [InlineData("a:b:c:d")]
    public void InvalidFormat_ProducesParseError(string invalidValue)
    {
        string[] args = ["--storage-service-connection", invalidValue];

        BuildOptions options = new();
        Command command = new("test", "test");
        command.AddOptions(options);

        ParseResult parseResult = command.Parse(args);

        parseResult.Errors.ShouldNotBeEmpty(
            $"Expected a parse error for invalid service connection format '{invalidValue}'");
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
