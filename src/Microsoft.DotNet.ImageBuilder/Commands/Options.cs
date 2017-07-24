// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class Options
    {
        protected abstract string CommandHelp { get; }
        protected abstract string CommandName { get; }

        public bool IsDryRun { get; set; }
        public string Manifest { get; set; }
        public string Repo { get; set; }
        public string RepoOwner { get; set; }
        public string Password { get; set; }
        public string Username { get; set; }

        public Options()
        {
        }

        public virtual ManifestFilter GetManifestFilter()
        {
            return new ManifestFilter()
            {
                IncludeRepo = Repo,
            };
        }

        public virtual void ParseCommandLine(ArgumentSyntax syntax)
        {
            ArgumentCommand command = syntax.DefineCommand(CommandName, this);
            command.Help = CommandHelp;

            bool isDryRun = false;
            syntax.DefineOption("dry-run", ref isDryRun, "Dry run of what images get built and order they would get built in");
            IsDryRun = isDryRun;

            string manifest = "manifest.json";
            syntax.DefineOption("manifest", ref manifest, "Path to json file which describes the repo");
            Manifest = manifest;

            string password = null;
            syntax.DefineOption("password", ref password, "Password for the Docker Registry the images are pushed to");
            Password = password;

            string repo = null;
            syntax.DefineOption("repo", ref repo, "Repo to operate on (Default is to all)");
            Repo = repo;

            string repoOwner = null;
            syntax.DefineOption("repo-owner", ref repoOwner, "An alternative repo owner which overrides what is specified in the manifest");
            RepoOwner = repoOwner;

            string username = null;
            syntax.DefineOption("username", ref username, "Username for the Docker Registry the images are pushed to");
            Username = username;
        }
    }
}
