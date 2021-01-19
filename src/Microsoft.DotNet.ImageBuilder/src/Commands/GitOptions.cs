﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.VersionTools.Automation;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
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
    }

    public class GitOptionsBuilder
    {
        public IEnumerable<Option> GetCliOptions(
            string? defaultOwner = null, string? defaultRepo = null, string? defaultBranch = null, string? defaultPath = null) =>
            new Option[]
            {
                CreateOption("git-branch", nameof(GitOptions.Branch),
                    $"GitHub branch to write to (defaults to '{defaultBranch}')", defaultBranch),
                CreateOption("git-owner", nameof(GitOptions.Owner),
                    $"Owner of the GitHub repo to write to (defaults to '{defaultOwner}')", defaultOwner),
                CreateOption("git-path", nameof(GitOptions.Path),
                    $"Path within the GitHub repo to write to (defaults to '{defaultPath}')", defaultPath),
                CreateOption("git-repo", nameof(GitOptions.Repo),
                    $"GitHub repo to write to (defaults to '{defaultRepo}')", defaultRepo)
            };

        public IEnumerable<Argument> GetCliArguments() =>
            new Argument[]
            {
                new Argument<string>(nameof(GitOptions.Username), "GitHub username"),
                new Argument<string>(nameof(GitOptions.Email), "GitHub email"),
                new Argument<string>(nameof(GitOptions.AuthToken), "GitHub authentication token")
            };
    }
}
#nullable disable
