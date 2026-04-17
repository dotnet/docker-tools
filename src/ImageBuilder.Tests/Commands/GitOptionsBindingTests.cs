// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        GitOptionsBuilder builder = GitOptionsBuilder.Build()
            .WithGitHubAuth(isRequired: true);

        ParseResult parseResult = ParseGitOptions(builder, []);

        parseResult.Errors.ShouldNotBeEmpty(
            "Expected a validation error when no GitHub auth is provided but isRequired is true");
    }

    [Fact]
    public void WithGitHubAuth_IsRequired_NoErrorWhenTokenProvided()
    {
        GitOptionsBuilder builder = GitOptionsBuilder.Build()
            .WithGitHubAuth(isRequired: true);

        ParseResult parseResult = ParseGitOptions(builder, ["--gh-token", "my-pat"]);

        parseResult.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void WithGitHubAuth_IsRequired_NoErrorWhenAppAuthProvided()
    {
        GitOptionsBuilder builder = GitOptionsBuilder.Build()
            .WithGitHubAuth(isRequired: true);

        ParseResult parseResult = ParseGitOptions(builder,
        [
            "--gh-private-key", "base64key",
            "--gh-app-client-id", "my-client-id",
        ]);

        parseResult.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void WithGitHubAuth_NotRequired_NoErrorWhenNoAuthProvided()
    {
        GitOptionsBuilder builder = GitOptionsBuilder.Build()
            .WithGitHubAuth(isRequired: false);

        ParseResult parseResult = ParseGitOptions(builder, []);

        parseResult.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void WithGitHubAuth_CustomDescription_AppliedToTokenOption()
    {
        string customDescription = "Custom auth description";

        GitOptionsBuilder builder = GitOptionsBuilder.Build()
            .WithGitHubAuth(description: customDescription);

        Command command = BuildCommand(builder);

        Option? tokenOption = command.Options
            .FirstOrDefault(o => o.Name == "--gh-token");

        tokenOption.ShouldNotBeNull();
        tokenOption.Description.ShouldBe(customDescription);
    }

    [Fact]
    public void WithGitHubAuth_DefaultDescription_UsedWhenNoneProvided()
    {
        GitOptionsBuilder builder = GitOptionsBuilder.Build()
            .WithGitHubAuth();

        Command command = BuildCommand(builder);

        Option? tokenOption = command.Options
            .FirstOrDefault(o => o.Name == "--gh-token");

        tokenOption.ShouldNotBeNull();
        tokenOption.Description.ShouldBe("GitHub Personal Access Token (PAT)");
    }

    /// <summary>
    /// Builds a command with the options and validators from the given <see cref="GitOptionsBuilder"/>
    /// and parses the provided args.
    /// </summary>
    private static ParseResult ParseGitOptions(GitOptionsBuilder builder, string[] args)
    {
        Command command = BuildCommand(builder);
        return command.Parse(args);
    }

    private static Command BuildCommand(GitOptionsBuilder builder)
    {
        Command command = new("test", "test");

        foreach (Option option in builder.GetCliOptions())
        {
            command.Add(option);
        }

        foreach (Argument argument in builder.GetCliArguments())
        {
            command.Add(argument);
        }

        foreach (Action<CommandResult> validator in builder.GetValidators())
        {
            command.Validators.Add(validator);
        }

        return command;
    }
}
