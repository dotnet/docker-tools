// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ImageBuilder.Commands;
using Shouldly;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.CommandLine.OptionsBindingTestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.CommandLine;

public class GitOptionsBindingTests
{
    [Fact]
    public void GitHubToken_BoundFromCliArgs()
    {
        string[] args = ["--gh-token", "my-pat"];
        TestGitOptions options = ParseAndBind<TestGitOptions>(args);

        options.GitOptions.GitHubAuthOptions.AuthToken.ShouldBe("my-pat");
        options.GitOptions.GitHubAuthOptions.PrivateKey.ShouldBeEmpty();
        options.GitOptions.GitHubAuthOptions.ClientId.ShouldBeEmpty();
        options.GitOptions.GitHubAuthOptions.InstallationId.ShouldBeNull();
        options.GitOptions.GitHubAuthOptions.HasCredentials.ShouldBeTrue();
        options.GitOptions.GitHubAuthOptions.IsGitHubAppAuth.ShouldBeFalse();
    }

    [Fact]
    public void GitHubAppAuth_BoundFromCliArgs()
    {
        string[] args =
        [
            "--gh-private-key", "base64key",
            "--gh-app-client-id", "my-client-id",
            "--gh-app-installation-id", "42",
        ];

        TestGitOptions options = ParseAndBind<TestGitOptions>(args);

        options.GitOptions.GitHubAuthOptions.AuthToken.ShouldBeEmpty();
        options.GitOptions.GitHubAuthOptions.PrivateKey.ShouldBe("base64key");
        options.GitOptions.GitHubAuthOptions.ClientId.ShouldBe("my-client-id");
        options.GitOptions.GitHubAuthOptions.InstallationId.ShouldBe("42");
        options.GitOptions.GitHubAuthOptions.HasCredentials.ShouldBeTrue();
        options.GitOptions.GitHubAuthOptions.IsGitHubAppAuth.ShouldBeTrue();
    }

    [Fact]
    public void GitRepositoryValues_BoundFromCliArgs()
    {
        string[] args =
        [
            "--git-owner", "dotnet",
            "--git-repo", "docker-tools",
            "--git-branch", "main",
            "--git-path", "eng/docker-tools",
        ];

        TestGitOptions options = ParseAndBind<TestGitOptions>(args);

        options.GitOptions.Owner.ShouldBe("dotnet");
        options.GitOptions.Repo.ShouldBe("docker-tools");
        options.GitOptions.Branch.ShouldBe("main");
        options.GitOptions.Path.ShouldBe("eng/docker-tools");
    }

    [Fact]
    public void GitUserValues_BoundFromCliArgs()
    {
        string[] args = ["--git-username", "bot", "--git-email", "bot@example.com"];
        TestGitOptions options = ParseAndBind<TestGitOptions>(args);

        options.GitOptions.Username.ShouldBe("bot");
        options.GitOptions.Email.ShouldBe("bot@example.com");
    }

    [Fact]
    public void GitHubAuth_DefaultsWhenNotSpecified()
    {
        TestGitOptions options = ParseAndBind<TestGitOptions>([]);

        options.GitOptions.GitHubAuthOptions.AuthToken.ShouldBeEmpty();
        options.GitOptions.GitHubAuthOptions.PrivateKey.ShouldBeEmpty();
        options.GitOptions.GitHubAuthOptions.ClientId.ShouldBeEmpty();
        options.GitOptions.GitHubAuthOptions.InstallationId.ShouldBeNull();
        options.GitOptions.GitHubAuthOptions.HasCredentials.ShouldBeFalse();
        options.GitOptions.GitHubAuthOptions.IsGitHubAppAuth.ShouldBeFalse();
    }

    [Fact]
    public void RequiredGitHubAuth_ProducesParseErrorWhenNoAuthProvided()
    {
        TestGitOptionsWithRequiredAuth options = new();
        ParseResult parseResult = Parse(options, args: []);

        parseResult.Errors.ShouldNotBeEmpty(
            "Expected a parse error when no GitHub auth is provided but auth is required");
    }

    [Fact]
    public void RequiredGitHubToken_BoundFromCliArgs()
    {
        string[] args = ["--gh-token", "my-pat"];
        TestGitOptionsWithRequiredAuth options = ParseAndBind<TestGitOptionsWithRequiredAuth>(args);

        options.GitOptions.GitHubAuthOptions.AuthToken.ShouldBe("my-pat");
        options.GitOptions.GitHubAuthOptions.HasCredentials.ShouldBeTrue();
    }

    [Fact]
    public void RequiredGitHubAppAuth_BoundFromCliArgs()
    {
        string[] args = ["--gh-private-key", "base64key", "--gh-app-client-id", "my-client-id"];
        TestGitOptionsWithRequiredAuth options = ParseAndBind<TestGitOptionsWithRequiredAuth>(args);

        options.GitOptions.GitHubAuthOptions.PrivateKey.ShouldBe("base64key");
        options.GitOptions.GitHubAuthOptions.ClientId.ShouldBe("my-client-id");
        options.GitOptions.GitHubAuthOptions.HasCredentials.ShouldBeTrue();
        options.GitOptions.GitHubAuthOptions.IsGitHubAppAuth.ShouldBeTrue();
    }

    /// <summary>
    /// Creates the all-optional Git CLI shape used by the general binding tests.
    /// </summary>
    private static GitOptionsBuilder CreateOptionalGitBuilder() =>
        GitOptionsBuilder.Build()
            .WithUsername()
            .WithEmail()
            .WithGitHubAuth()
            .WithBranch()
            .WithOwner()
            .WithPath()
            .WithRepo();

    /// <summary>
    /// Creates the Git CLI shape used by the required-auth tests, where GitHub authentication must
    /// be supplied but the remaining Git symbols stay optional.
    /// </summary>
    private static GitOptionsBuilder CreateRequiredGitBuilder() =>
        GitOptionsBuilder.Build()
            .WithUsername()
            .WithEmail()
            .WithGitHubAuth(isRequired: true)
            .WithBranch()
            .WithOwner()
            .WithPath()
            .WithRepo();

    /// <summary>
    /// Test-only wrapper that exposes the standard, fully optional Git CLI shape so binding tests
    /// can parse args and assert against the resulting <see cref="GitOptions"/> object.
    /// </summary>
    private sealed class TestGitOptions() : TestGitOptionsBase(CreateOptionalGitBuilder());

    /// <summary>
    /// Test-only wrapper that uses the Git CLI shape where GitHub authentication is required so
    /// the same end-to-end parsing path can verify required-auth validation behavior.
    /// </summary>
    private sealed class TestGitOptionsWithRequiredAuth() : TestGitOptionsBase(CreateRequiredGitBuilder());

    /// <summary>
    /// Shared test <see cref="Options"/> implementation that wires a
    /// <see cref="GitOptionsBuilder"/> into the normal command registration and binding flow, then
    /// exposes the bound <see cref="GitOptions"/> instance for assertions.
    /// </summary>
    /// <param name="gitBuilder">
    /// The Git options builder that defines which Git-specific CLI symbols and validators this test
    /// wrapper should expose.
    /// </param>
    private abstract class TestGitOptionsBase(GitOptionsBuilder gitBuilder) : Options
    {
        /// <summary>
        /// Gets the bound Git options instance populated from the parsed command line values.
        /// </summary>
        public GitOptions GitOptions { get; } = new();

        /// <summary>
        /// Adds the Git-specific options from the supplied builder alongside the base test options.
        /// </summary>
        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..gitBuilder.GetCliOptions(),
        ];

        /// <summary>
        /// Adds any Git-specific positional arguments exposed by the supplied builder.
        /// </summary>
        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..gitBuilder.GetCliArguments(),
        ];

        /// <summary>
        /// Exposes any Git-specific validators so parse-time validation matches the production
        /// command behavior.
        /// </summary>
        public override IEnumerable<Action<CommandResult>> GetValidators() =>
        [
            ..base.GetValidators(),
            ..gitBuilder.GetValidators(),
        ];

        /// <summary>
        /// Binds both the shared base options and the test's Git-specific options from the parsed
        /// command line values.
        /// </summary>
        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            gitBuilder.Bind(result, GitOptions);
        }
    }
}
