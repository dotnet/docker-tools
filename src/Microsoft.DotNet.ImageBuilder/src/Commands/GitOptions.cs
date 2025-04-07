// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.VersionTools.Automation;
using Octokit;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
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

    public record GitHubAuthOptions(string AuthToken = "", string PrivateKeyFilePath = "")
    {
        public bool IsPrivateKeyAuth => !string.IsNullOrEmpty(PrivateKeyFilePath);

        public bool HasCredentials => !string.IsNullOrEmpty(AuthToken) || !string.IsNullOrEmpty(PrivateKeyFilePath);
    }

    public class GitOptionsBuilder
    {
        private readonly List<Option> _options = new();
        private readonly List<Argument> _arguments = new();

        private GitOptionsBuilder()
        {
        }

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
            description ??= "GitHub Personal Access Token (PAT) or private key file (.pem)";
            description += " [token=<token> | private-key-file=<path to .pem file>]";

            _options.Add(CreateOption(
                "github-auth",
                nameof(GitOptions.GitHubAuthOptions),
                "GitHub Personal Access Token (PAT) or private key file (.pem) [token=<token> | private-key-file=<path to .pem file>]",
                isRequired: isRequired,
                parseArg: argumentResult =>
                {
                    var dictionary = argumentResult.Tokens
                        .Select(token => token.Value.ParseKeyValuePair('='))
                        .ToDictionary();

                    string token = dictionary.GetValueOrDefault("token", "");
                    string privateKeyFile = dictionary.GetValueOrDefault("private-key-file", "");

                    // While the command will fail if the option is not provided, it doesn't mean that the correct
                    // key-value pair was provided. So we need to check that at least one of the two expected values
                    // is provided. We don't need to check for mutual exclusivity, since only one argument will be
                    // accepted.
                    if (isRequired && string.IsNullOrEmpty(token) && string.IsNullOrEmpty(privateKeyFile))
                    {
                        throw new ArgumentException("GitHub token or private key file must be provided.");
                    }

                    return new GitHubAuthOptions(token, privateKeyFile);
                }));

            return this;
        }

        public IEnumerable<Option> GetCliOptions() => _options;

        public IEnumerable<Argument> GetCliArguments() => _arguments;

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
