// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GitOptions : IGitHubFileRef
    {
        public string Branch { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Repo { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        public Uri GetRepoUrl() => new Uri($"https://github.com/{Owner}/{Repo}");

        public GitHubAuthOptions GitHubAuthOptions { get; set; } = new GitHubAuthOptions();
    }

    public record GitHubAuthOptions(
        string AuthToken = "",
        string PrivateKey = "",
        string ClientId = "",
        string? InstallationId = null)
    {
        public bool IsGitHubAppAuth =>
            !string.IsNullOrEmpty(PrivateKey) &&
            !string.IsNullOrEmpty(ClientId);

        public bool HasCredentials =>
            !string.IsNullOrEmpty(AuthToken) ||
            (!string.IsNullOrEmpty(PrivateKey) && !string.IsNullOrEmpty(ClientId));
    }

    public class GitOptionsBuilder : CliOptionsBuilder
    {
        private readonly List<Option> _options = [];

        private readonly List<Argument> _arguments = [];

        private readonly List<ValidateSymbol<CommandResult>> _validators = [];

        private GitOptionsBuilder()
        {
        }

        public override IEnumerable<Option> GetCliOptions() => _options;

        public override IEnumerable<Argument> GetCliArguments() => _arguments;

        public override IEnumerable<ValidateSymbol<CommandResult>> GetValidators() => _validators;

        public static GitOptionsBuilder Build() => new();

        public static GitOptionsBuilder BuildWithDefaults() =>
            Build()
                .WithUsername(isRequired: true)
                .WithEmail(isRequired: true)
                .WithGitHubAuth()
                .WithBranch()
                .WithOwner()
                .WithPath()
                .WithRepo();

        public GitOptionsBuilder WithUsername(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Username to use for GitHub connection") =>
            AddSymbol("git-username", nameof(GitOptions.Username), isRequired, defaultValue, description);

        public GitOptionsBuilder WithEmail(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Email to use for GitHub connection") =>
            AddSymbol("git-email", nameof(GitOptions.Email), isRequired, defaultValue, description);

        public GitOptionsBuilder WithBranch(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Name of GitHub branch to access") =>
            AddSymbol("git-branch", nameof(GitOptions.Branch), isRequired, defaultValue, description);

        public GitOptionsBuilder WithOwner(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Owner of the GitHub repo to access") =>
            AddSymbol("git-owner", nameof(GitOptions.Owner), isRequired, defaultValue, description);

        public GitOptionsBuilder WithPath(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Path of the GitHub repo to access") =>
            AddSymbol("git-path", nameof(GitOptions.Path), isRequired, defaultValue, description);

        public GitOptionsBuilder WithRepo(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Name of the GitHub repo to access") =>
            AddSymbol("git-repo", nameof(GitOptions.Repo), isRequired, defaultValue, description);

        public GitOptionsBuilder WithGitHubAuth(string? description = null, bool isRequired = false)
        {
            const string TokenAlias = "gh-token";
            var tokenOption = CreateOption<string>(
                TokenAlias,
                nameof(GitHubAuthOptions.AuthToken),
                "GitHub Personal Access Token (PAT)");

            const string PrivateKeyAlias = "gh-private-key";
            var privateKeyOption = CreateOption<string>(
                PrivateKeyAlias,
                nameof(GitHubAuthOptions.PrivateKey),
                "Base64-encoded private key (pem format) for GitHub App authentication");

            const string ClientIdAlias = "gh-app-client-id";
            var clientIdOption = CreateOption<string>(
                ClientIdAlias,
                nameof(GitHubAuthOptions.ClientId),
                "GitHub Client ID for GitHub App authentication");

            const string InstallationIdAlias = "gh-app-installation-id";
            var installationIdOption = CreateOption<string?>(
                InstallationIdAlias,
                nameof(GitHubAuthOptions.InstallationId),
                "GitHub App installation ID to use (only required if app has more than one installation)");

            _options.AddRange([tokenOption, privateKeyOption, clientIdOption, installationIdOption]);

            _validators.Add(command =>
                {
                    var hasToken = command.Has(tokenOption);
                    var hasPrivateKey = command.Has(privateKeyOption);
                    var hasClientId = command.Has(clientIdOption);

                    // If token is provided, ensure that private key and client ID were not provided
                    if (hasToken && (hasPrivateKey || hasClientId))
                    {
                        return "Authentication conflict: Cannot use both GitHub personal access token "
                            + $"({FormatAlias(TokenAlias)}) and GitHub App credentials ({FormatAlias(PrivateKeyAlias)} "
                            + $"and {FormatAlias(ClientIdAlias)}) simultaneously. Please provide only one authentication "
                            + "method.";
                    }

                    // Both client ID and private key file are required for GitHub App authentication
                    if (hasPrivateKey != hasClientId)
                    {
                        return $"GitHub App authentication requires both {FormatAlias(ClientIdAlias)} "
                            + $"and {FormatAlias(PrivateKeyAlias)} but only one was provided.";
                    }

                    // Returning null indicates that validation passed
                    return null;
                });

            return this;
        }

        private GitOptionsBuilder AddSymbol<T>(
            string alias,
            string propertyName,
            bool isRequired,
            T? defaultValue,
            string description)
        {
            if (isRequired)
            {
                _arguments.Add(new Argument<T>(propertyName, description));
            }
            else
            {
                _options.Add(
                    CreateOption(
                        alias,
                        propertyName,
                        description,
                        defaultValue is null ? default! : defaultValue));
            }

            return this;
        }
    }
}
