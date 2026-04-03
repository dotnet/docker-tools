// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

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

    public class GitOptionsBuilder
    {
        private readonly List<Option> _options = [];
        private readonly List<Argument> _arguments = [];
        private readonly List<Action<CommandResult>> _validators = [];

        private Option<string>? _usernameOption;
        private Argument<string>? _usernameArgument;
        private Option<string>? _emailOption;
        private Argument<string>? _emailArgument;
        private Option<string>? _branchOption;
        private Argument<string>? _branchArgument;
        private Option<string>? _ownerOption;
        private Argument<string>? _ownerArgument;
        private Option<string>? _pathOption;
        private Argument<string>? _pathArgument;
        private Option<string>? _repoOption;
        private Argument<string>? _repoArgument;
        private Option<string>? _tokenOption;
        private Option<string>? _privateKeyOption;
        private Option<string>? _clientIdOption;
        private Option<string?>? _installationIdOption;

        private GitOptionsBuilder()
        {
        }

        public IEnumerable<Option> GetCliOptions() => _options;

        public IEnumerable<Argument> GetCliArguments() => _arguments;

        public IEnumerable<Action<CommandResult>> GetValidators() => _validators;

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
            AddSymbol("git-username", nameof(GitOptions.Username), isRequired, defaultValue, description,
                opt => _usernameOption = opt, arg => _usernameArgument = arg);

        public GitOptionsBuilder WithEmail(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Email to use for GitHub connection") =>
            AddSymbol("git-email", nameof(GitOptions.Email), isRequired, defaultValue, description,
                opt => _emailOption = opt, arg => _emailArgument = arg);

        public GitOptionsBuilder WithBranch(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Name of GitHub branch to access") =>
            AddSymbol("git-branch", nameof(GitOptions.Branch), isRequired, defaultValue, description,
                opt => _branchOption = opt, arg => _branchArgument = arg);

        public GitOptionsBuilder WithOwner(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Owner of the GitHub repo to access") =>
            AddSymbol("git-owner", nameof(GitOptions.Owner), isRequired, defaultValue, description,
                opt => _ownerOption = opt, arg => _ownerArgument = arg);

        public GitOptionsBuilder WithPath(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Path of the GitHub repo to access") =>
            AddSymbol("git-path", nameof(GitOptions.Path), isRequired, defaultValue, description,
                opt => _pathOption = opt, arg => _pathArgument = arg);

        public GitOptionsBuilder WithRepo(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Name of the GitHub repo to access") =>
            AddSymbol("git-repo", nameof(GitOptions.Repo), isRequired, defaultValue, description,
                opt => _repoOption = opt, arg => _repoArgument = arg);

        public GitOptionsBuilder WithGitHubAuth(string? description = null, bool isRequired = false)
        {
            const string TokenAlias = "gh-token";
            _tokenOption = new Option<string>(CliHelper.FormatAlias(TokenAlias))
            {
                Description = "GitHub Personal Access Token (PAT)"
            };

            const string PrivateKeyAlias = "gh-private-key";
            _privateKeyOption = new Option<string>(CliHelper.FormatAlias(PrivateKeyAlias))
            {
                Description = "Base64-encoded private key (pem format) for GitHub App authentication"
            };

            const string ClientIdAlias = "gh-app-client-id";
            _clientIdOption = new Option<string>(CliHelper.FormatAlias(ClientIdAlias))
            {
                Description = "GitHub Client ID for GitHub App authentication"
            };

            const string InstallationIdAlias = "gh-app-installation-id";
            _installationIdOption = new Option<string?>(CliHelper.FormatAlias(InstallationIdAlias))
            {
                Description = "GitHub App installation ID to use (only required if app has more than one installation)"
            };

            _options.AddRange([_tokenOption, _privateKeyOption, _clientIdOption, _installationIdOption]);

            _validators.Add(command =>
                {
                    bool hasToken = command.Has(_tokenOption);
                    bool hasPrivateKey = command.Has(_privateKeyOption);
                    bool hasClientId = command.Has(_clientIdOption);

                    // If token is provided, ensure that private key and client ID were not provided
                    if (hasToken && (hasPrivateKey || hasClientId))
                    {
                        command.AddError("Authentication conflict: Cannot use both GitHub personal access token "
                            + $"({CliHelper.FormatAlias(TokenAlias)}) and GitHub App credentials ({CliHelper.FormatAlias(PrivateKeyAlias)} "
                            + $"and {CliHelper.FormatAlias(ClientIdAlias)}) simultaneously. Please provide only one authentication "
                            + "method.");
                        return;
                    }

                    // Both client ID and private key file are required for GitHub App authentication
                    if (hasPrivateKey != hasClientId)
                    {
                        command.AddError($"GitHub App authentication requires both {CliHelper.FormatAlias(ClientIdAlias)} "
                            + $"and {CliHelper.FormatAlias(PrivateKeyAlias)} but only one was provided.");
                    }
                });

            return this;
        }

        /// <summary>
        /// Binds parsed command line values to the specified <see cref="GitOptions"/> instance.
        /// </summary>
        public void Bind(ParseResult result, GitOptions target)
        {
            if (_usernameOption is not null)
                target.Username = result.GetValue(_usernameOption) ?? string.Empty;
            else if (_usernameArgument is not null)
                target.Username = result.GetValue(_usernameArgument) ?? string.Empty;

            if (_emailOption is not null)
                target.Email = result.GetValue(_emailOption) ?? string.Empty;
            else if (_emailArgument is not null)
                target.Email = result.GetValue(_emailArgument) ?? string.Empty;

            if (_branchOption is not null)
                target.Branch = result.GetValue(_branchOption) ?? string.Empty;
            else if (_branchArgument is not null)
                target.Branch = result.GetValue(_branchArgument) ?? string.Empty;

            if (_ownerOption is not null)
                target.Owner = result.GetValue(_ownerOption) ?? string.Empty;
            else if (_ownerArgument is not null)
                target.Owner = result.GetValue(_ownerArgument) ?? string.Empty;

            if (_pathOption is not null)
                target.Path = result.GetValue(_pathOption) ?? string.Empty;
            else if (_pathArgument is not null)
                target.Path = result.GetValue(_pathArgument) ?? string.Empty;

            if (_repoOption is not null)
                target.Repo = result.GetValue(_repoOption) ?? string.Empty;
            else if (_repoArgument is not null)
                target.Repo = result.GetValue(_repoArgument) ?? string.Empty;

            if (_tokenOption is not null)
            {
                target.GitHubAuthOptions = new GitHubAuthOptions(
                    AuthToken: result.GetValue(_tokenOption) ?? string.Empty,
                    PrivateKey: result.GetValue(_privateKeyOption!) ?? string.Empty,
                    ClientId: result.GetValue(_clientIdOption!) ?? string.Empty,
                    InstallationId: _installationIdOption is not null ? result.GetValue(_installationIdOption) : null);
            }
        }

        private GitOptionsBuilder AddSymbol<T>(
            string alias,
            string argumentName,
            bool isRequired,
            T? defaultValue,
            string description,
            Action<Option<T>> storeOption,
            Action<Argument<T>> storeArgument)
        {
            if (isRequired)
            {
                Argument<T> argument = new(argumentName) { Description = description };
                _arguments.Add(argument);
                storeArgument(argument);
            }
            else
            {
                Option<T> option = new(CliHelper.FormatAlias(alias))
                {
                    Description = description,
                    DefaultValueFactory = _ => defaultValue is null ? default! : defaultValue
                };
                _options.Add(option);
                storeOption(option);
            }

            return this;
        }
    }
}
