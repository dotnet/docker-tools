// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GitOptions
    {
        private readonly string defaultOwner;
        private readonly string defaultRepo;
        private readonly string defaultBranch;
        private readonly string defaultPath;

        public string AuthToken { get; set; }
        public string Branch { get; set; }
        public string Email { get; set; }
        public string Owner { get; set; }
        public string Path { get; set; }
        public string Repo { get; set; }
        public string Username { get; set; }

        public GitOptions(string defaultOwner, string defaultRepo, string defaultBranch, string defaultPath)
        {
            this.defaultOwner = defaultOwner ?? throw new ArgumentNullException(nameof(defaultOwner));
            this.defaultRepo = defaultRepo ?? throw new ArgumentNullException(nameof(defaultRepo));
            this.defaultBranch = defaultBranch ?? throw new ArgumentNullException(nameof(defaultBranch));
            this.defaultPath = defaultPath ?? throw new ArgumentNullException(nameof(defaultPath));
        }

        public void ParseCommandLine(ArgumentSyntax syntax)
        {
            string branch = this.defaultBranch;
            syntax.DefineOption(
                "git-branch",
                ref branch,
                $"GitHub branch to write to (defaults to {this.defaultBranch})");
            Branch = branch;

            string owner = this.defaultOwner;
            syntax.DefineOption(
                "git-owner",
                ref owner,
                $"Owner of the GitHub repo to write to (defaults to {this.defaultOwner})");
            Owner = owner;

            string path = this.defaultPath;
            syntax.DefineOption(
                "git-path",
                ref path,
                $"Path within the GitHub repo to write to (defaults to {this.defaultPath})");
            Path = path;

            string repo = this.defaultRepo;
            syntax.DefineOption(
                "git-repo",
                ref repo,
                $"GitHub repo to write to (defaults to {this.defaultRepo})");
            Repo = repo;

            string username = null;
            syntax.DefineParameter(
                "git-username",
                ref username,
                "GitHub username");
            Username = username;

            string email = null;
            syntax.DefineParameter(
                "git-email",
                ref email,
                "GitHub email");
            Email = email;

            string authToken = null;
            syntax.DefineParameter(
                "git-auth-token",
                ref authToken,
                "GitHub authentication token");
            AuthToken = authToken;
        }
    }
}
