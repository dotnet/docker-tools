// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

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
    private readonly List<Action<ParseResult, GitOptions>> _binders = [];

    private GitOptionsBuilder()
    {
    }

    public IEnumerable<Option> GetCliOptions() => _options;

    public IEnumerable<Argument> GetCliArguments() => _arguments;

    public IEnumerable<Action<CommandResult>> GetValidators() => _validators;

    public static GitOptionsBuilder Build() => new();

    public static GitOptionsBuilder BuildForRepositoryOperations() =>
        Build()
            .WithUsername(isRequired: true)
            .WithEmail(isRequired: true)
            .WithGitHubAuth()
            .WithBranch()
            .WithOwner()
            .WithPath()
            .WithRepo();

    public static GitOptionsBuilder BuildWithDefaults() =>
        BuildForRepositoryOperations();

    public GitOptionsBuilder WithUsername(
        bool isRequired = false,
        string description = "Username to use for GitHub connection") =>
            AddStringSymbol(
                optionName: "--git-username",
                argumentName: nameof(GitOptions.Username),
                isRequired: isRequired,
                description: description,
                bindValue: static (target, value) => target.Username = value);

    public GitOptionsBuilder WithEmail(
        bool isRequired = false,
        string description = "Email to use for GitHub connection") =>
            AddStringSymbol(
                optionName: "--git-email",
                argumentName: nameof(GitOptions.Email),
                isRequired: isRequired,
                description: description,
                bindValue: static (target, value) => target.Email = value);

    public GitOptionsBuilder WithBranch(
        bool isRequired = false,
        string description = "Name of GitHub branch to access") =>
            AddStringSymbol(
                optionName: "--git-branch",
                argumentName: nameof(GitOptions.Branch),
                isRequired: isRequired,
                description: description,
                bindValue: static (target, value) => target.Branch = value);

    public GitOptionsBuilder WithOwner(
        bool isRequired = false,
        string description = "Owner of the GitHub repo to access") =>
            AddStringSymbol(
                optionName: "--git-owner",
                argumentName: nameof(GitOptions.Owner),
                isRequired: isRequired,
                description: description,
                bindValue: static (target, value) => target.Owner = value);

    public GitOptionsBuilder WithPath(
        bool isRequired = false,
        string description = "Path of the GitHub repo to access") =>
            AddStringSymbol(
                optionName: "--git-path",
                argumentName: nameof(GitOptions.Path),
                isRequired: isRequired,
                description: description,
                bindValue: static (target, value) => target.Path = value);

    public GitOptionsBuilder WithRepo(
        bool isRequired = false,
        string description = "Name of the GitHub repo to access") =>
            AddStringSymbol(
                optionName: "--git-repo",
                argumentName: nameof(GitOptions.Repo),
                isRequired: isRequired,
                description: description,
                bindValue: static (target, value) => target.Repo = value);

    public GitOptionsBuilder WithGitHubAuth(string? description = null, bool isRequired = false)
    {
        Option<string> tokenOption = new("--gh-token")
        {
            Description = description ?? "GitHub Personal Access Token (PAT)"
        };

        Option<string> privateKeyOption = new("--gh-private-key")
        {
            Description = "Base64-encoded private key (pem format) for GitHub App authentication"
        };

        Option<string> clientIdOption = new("--gh-app-client-id")
        {
            Description = "GitHub Client ID for GitHub App authentication"
        };

        Option<string?> installationIdOption = new("--gh-app-installation-id")
        {
            Description = "GitHub App installation ID to use (only required if app has more than one installation)"
        };

        _options.AddRange([tokenOption, privateKeyOption, clientIdOption, installationIdOption]);

        _validators.Add((CommandResult commandResult) =>
            {
                bool hasToken = commandResult.Has(tokenOption);
                bool hasPrivateKey = commandResult.Has(privateKeyOption);
                bool hasClientId = commandResult.Has(clientIdOption);

                // If token is provided, ensure that private key and client ID were not provided
                if (hasToken && (hasPrivateKey || hasClientId))
                {
                    commandResult.AddError(
                        "Authentication conflict: Cannot use both GitHub personal access token ({tokenOption.Name})"
                        + $" and GitHub App credentials ({privateKeyOption.Name} and {clientIdOption.Name})"
                        + $" simultaneously. Please provide only one authentication method.");
                    return;
                }

                // Both client ID and private key file are required for GitHub App authentication
                if (hasPrivateKey != hasClientId)
                {
                    commandResult.AddError(
                        $"GitHub App authentication requires both {clientIdOption.Name} and {privateKeyOption.Name}"
                        + $" but only one was provided.");
                    return;
                }

                // When auth is required, at least one auth method must be provided
                if (isRequired && !hasToken && !hasPrivateKey)
                {
                    commandResult.AddError(
                        $"GitHub authentication is required. Provide either a personal access token"
                        + $" ({tokenOption.Name}) or GitHub App credentials ({privateKeyOption.Name} and"
                        + $" {clientIdOption.Name}).");
                }
            });

        _binders.Add((parseResult, target) =>
            target.GitHubAuthOptions =
                new GitHubAuthOptions(
                    AuthToken: parseResult.GetValue(tokenOption) ?? string.Empty,
                    PrivateKey: parseResult.GetValue(privateKeyOption) ?? string.Empty,
                    ClientId: parseResult.GetValue(clientIdOption) ?? string.Empty,
                    InstallationId: parseResult.GetValue(installationIdOption)));

        return this;
    }

    /// <summary>
    /// Binds parsed command line values to the specified <see cref="GitOptions"/> instance.
    /// </summary>
    public void Bind(ParseResult result, GitOptions target)
    {
        foreach (Action<ParseResult, GitOptions> bind in _binders)
        {
            bind(result, target);
        }
    }

    private GitOptionsBuilder AddStringSymbol(
        string optionName,
        string argumentName,
        bool isRequired,
        string description,
        Action<GitOptions, string> bindValue)
    {
        if (isRequired)
        {
            Argument<string> argument = new(argumentName) { Description = description };
            _arguments.Add(argument);
            _binders.Add((result, target) => bindValue(target, result.GetValue(argument) ?? string.Empty));
            return this;
        }

        Option<string> option = new(optionName) { Description = description };
        _options.Add(option);
        _binders.Add((result, target) => bindValue(target, result.GetValue(option) ?? string.Empty));
        return this;
    }
}
