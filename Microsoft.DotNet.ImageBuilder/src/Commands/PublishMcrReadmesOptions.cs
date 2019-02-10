// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishMcrReadmesOptions : Options
    {
        protected override string CommandHelp => "Publishes the readmes to MCR";

        public string GitAuthToken { get; set; }
        public string GitBranch { get; set; }
        public string GitEmail { get; set; }
        public string GitOwner { get; set; }
        public string GitPath { get; set; }
        public string GitRepo { get; set; }
        public string GitUsername { get; set; }

        public PublishMcrReadmesOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            string gitBranch = "master";
            syntax.DefineOption(
                "git-branch",
                ref gitBranch,
                "GitHub branch to write readmes to (defaults to master)");
            GitBranch = gitBranch;

            string gitOwner = "Microsoft";
            syntax.DefineOption(
                "git-owner",
                ref gitOwner,
                "Owner of the GitHub repo to write readmes to (defaults to Microsoft)");
            GitOwner = gitOwner;

            string gitPath = "teams";
            syntax.DefineOption(
                "git-path",
                ref gitPath,
                "Path within the GitHub repo to write readmes to (defaults to teams)");
            GitPath = gitPath;

            string gitRepo = "mcrdocs";
            syntax.DefineOption(
                "git-repo",
                ref gitRepo,
                "GitHub repo to readmes to (defaults to mcrdocs)");
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
