// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.DockerTools.ImageBuilder;
using Microsoft.DotNet.VersionTools.Automation;
using Octokit;
using static Microsoft.DotNet.DockerTools.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class GitOptions : IGitHubFileRef
    {
        public string AuthToken { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Repo { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        public GitHubAuth ToGitHubAuth()
        {
            return new GitHubAuth(AuthToken, Username, Email);
        }

        public Credentials ToOctokitCredentials() => new Credentials(AuthToken);

        public Uri GetRepoUrl() => new Uri($"https://github.com/{Owner}/{Repo}");
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
                .WithAuthToken(isRequired: true)
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

        public GitOptionsBuilder WithAuthToken(
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Auth token to use to connect to GitHub") =>
            AddSymbol("git-token", nameof(GitOptions.AuthToken), isRequired, defaultValue, description);

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

        public IEnumerable<Option> GetCliOptions() => _options;

        public IEnumerable<Argument> GetCliArguments() => _arguments;

        private GitOptionsBuilder AddSymbol<T>(string alias, string propertyName, bool isRequired, T? defaultValue, string description)
        {
            if (isRequired)
            {
                _arguments.Add(new Argument<T>(propertyName, description));
            }
            else
            {
                _options.Add(CreateOption(alias, propertyName, description, defaultValue is null ? default! : defaultValue));
            }

            return this;
        }
    }
}
#nullable disable
