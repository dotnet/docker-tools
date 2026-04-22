// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Commands;
using Shouldly;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.CommandLine.OptionsBindingTestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.CommandLine;

public class ServiceConnectionOptionsBindingTests
{
    [Fact]
    public void ValidFormat_ParsesCorrectly()
    {
        string[] args = ["--storage-service-connection", "my-tenant:my-client:my-connection-id"];
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
        ParseResult parseResult = Parse(options, args);

        parseResult.Errors.ShouldNotBeEmpty(
            $"Expected a parse error for invalid service connection format '{invalidValue}'");
    }
}
