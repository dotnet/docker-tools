// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateImageDataOptions : Options
    {
        protected override string CommandHelp => "Updates an image data file in GitHub by merging in the image data that was generated from a build.";

        public string GitUsername { get; set; }
        public string GitEmail { get; set; }
        public string GitAuthToken { get; set; }
        public string GitOwner { get; set; }
        public string GitRepo { get; set; }
        public string GitBranch { get; set; }
        public string GitImageDataPath { get; set; }
        public string SourceImageDataFolderPath { get; set; }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            string gitUsername = null;
            syntax.DefineOption(
                "git-username",
                ref gitUsername,
                "GitHub username");
            GitUsername = gitUsername;

            string gitEmail = null;
            syntax.DefineOption(
                "git-email",
                ref gitEmail,
                "GitHub email");
            GitEmail = gitEmail;

            string gitAuthToken = null;
            syntax.DefineOption(
                "git-auth-token",
                ref gitAuthToken,
                "GitHub authentication token");
            GitAuthToken = gitAuthToken;

            string gitOwner = null;
            syntax.DefineOption(
                "git-owner",
                ref gitOwner,
                "GitHub owner");
            GitOwner = gitOwner;

            string gitRepo = null;
            syntax.DefineOption(
                "git-repo",
                ref gitRepo,
                "GitHub repo");
            GitRepo = gitRepo;

            string gitBranch = null;
            syntax.DefineOption(
                "git-branch",
                ref gitBranch,
                "GitHub branch");
            GitBranch = gitBranch;

            string gitImageDataPath = null;
            syntax.DefineOption(
                "git-image-data-path",
                ref gitImageDataPath,
                "Path to the image data file in GitHub.");
            GitImageDataPath = gitImageDataPath;

            string sourceImageDataFolderPath = null;
            syntax.DefineOption(
                "source-image-data-folder-path",
                ref sourceImageDataFolderPath,
                "Path to the folder where one or more local image data files are stored and are to be merged into GitHub.");
            SourceImageDataFolderPath = sourceImageDataFolderPath;
        }
    }
}
