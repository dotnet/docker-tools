// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.IO;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateVersionsOptions : Options
    {
        protected override string CommandHelp => "Updates the version information for the dependent images";
        protected override string CommandName => "updateVersions";

        public string GitAuthToken { get; set; }
        public string GitBranch { get; set; }
        public string GitEmail { get; set; }
        public string GitOwner { get; set; }
        public string GitPath { get; set; }
        public string GitRepo { get; set; }
        public string GitUsername { get; set; }

        public UpdateVersionsOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            string gitBranch = "master";
            syntax.DefineOption(
                "git-branch",
                ref gitBranch,
                "GitHub branch to write version info to");
            GitBranch = gitBranch;

            string gitOwner = "dotnet";
            syntax.DefineOption(
                "git-owner",
                ref gitOwner,
                "Owner of the GitHub repo to write version info to");
            GitOwner = gitOwner;

            string gitPath = "build-info/external/docker";
            syntax.DefineOption(
                "git-path",
                ref gitPath,
                "Path within the GitHub repo to write version info to");
            GitPath = gitPath;

            string gitRepo = "versions";
            syntax.DefineOption(
                "git-repo",
                ref gitRepo,
                "GitHub repo to write version info to");
            GitRepo = gitRepo;

            string gitUsername = null;
            syntax.DefineParameter(
                "git-username",
                ref gitUsername,
                "GitHub username");
            GitUsername = gitUsername;

            string gitEmail = null;
            syntax.DefineParameter(
                "git-email",
                ref gitEmail,
                "GitHub email");
            GitEmail = gitEmail;

            string gitAuthToken = null;
            syntax.DefineParameter(
                "git-auth-token",
                ref gitAuthToken,
                "GitHub authentication token");
            GitAuthToken = gitAuthToken;
        }
    }
}
