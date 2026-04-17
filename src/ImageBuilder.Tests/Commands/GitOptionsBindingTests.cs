// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Commands;

public class GitOptionsBindingTests
{
    [Fact]
    public void WithGitHubAuth_IsRequired_ProducesErrorWhenNoAuthProvided()
    {
        // PostPublishNotificationOptions uses WithGitHubAuth(isRequired: true).
        // When no auth is provided, parsing should produce a validation error.
        string[] args =
        [
            // PostPublishNotificationOptions positional args:
            // SourceRepo, SourceBranch, ImageInfoPath, BuildId,
            // AzdoOptions: AccessToken, Organization, Project
            // GitBuilder: Owner, Repo
            "my-repo", "main", "image-info.json", "12345",
            "azdo-token", "my-org", "my-project",
            "dotnet", "docker-tools",
        ];

        PostPublishNotificationOptions options = new();
        Command command = new("test", "test");
        command.AddOptions(options);

        ParseResult parseResult = command.Parse(args);

        parseResult.Errors.ShouldNotBeEmpty(
            "Expected a validation error when no GitHub auth is provided but isRequired is true");
    }

    [Fact]
    public void WithGitHubAuth_IsRequired_NoErrorWhenTokenProvided()
    {
        string[] args =
        [
            "my-repo", "main", "image-info.json", "12345",
            "azdo-token", "my-org", "my-project",
            "dotnet", "docker-tools",
            "--gh-token", "my-pat",
        ];

        PostPublishNotificationOptions options = new();
        Command command = new("test", "test");
        command.AddOptions(options);

        ParseResult parseResult = command.Parse(args);

        parseResult.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void WithGitHubAuth_IsRequired_NoErrorWhenAppAuthProvided()
    {
        string[] args =
        [
            "my-repo", "main", "image-info.json", "12345",
            "azdo-token", "my-org", "my-project",
            "dotnet", "docker-tools",
            "--gh-private-key", "base64key",
            "--gh-app-client-id", "my-client-id",
        ];

        PostPublishNotificationOptions options = new();
        Command command = new("test", "test");
        command.AddOptions(options);

        ParseResult parseResult = command.Parse(args);

        parseResult.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void WithGitHubAuth_CustomDescription_AppliedToTokenOption()
    {
        string customDescription = "Auth token to use to connect to GitHub for posting notifications";

        // PostPublishNotificationOptions uses WithGitHubAuth with a custom description
        PostPublishNotificationOptions options = new();
        Command command = new("test", "test");
        command.AddOptions(options);

        Option? tokenOption = command.Options
            .FirstOrDefault(o => o.Name == "--gh-token");

        tokenOption.ShouldNotBeNull();
        tokenOption.Description.ShouldBe(customDescription);
    }
}
