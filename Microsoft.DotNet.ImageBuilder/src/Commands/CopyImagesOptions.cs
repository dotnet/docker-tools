// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyImagesOptions : Options, IManifestFilterOptions
    {
        protected override string CommandHelp => "Copies the platform images as specified in the manifest between Docker registries";
        protected override string CommandName => "copyImages";

        public Architecture Architecture { get; set; }
        public string DestinationPassword { get; set; }
        public string DestinationServer { get; set; }
        public string DestinationUsername { get; set; }
        public IEnumerable<string> Paths { get; set; }
        public string OsVersion { get; set; }
        public string SourcePassword { get; set; }
        public string SourceRepo { get; set; }
        public string SourceServer { get; set; }
        public string SourceUsername { get; set; }

        public CopyImagesOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            DefineManifestFilterOptions(syntax, this);

            string destinationPassword = null;
            Argument<string> destinationPasswordArg = syntax.DefineOption(
                "destination-password",
                ref destinationPassword,
                "Password for the Docker Registry the images are pushed to");
            DestinationPassword = destinationPassword;

            string destinationServer = null;
            syntax.DefineOption(
                "destination-server",
                ref destinationServer,
                "Docker Registry server the images are pushed to (default is Docker Hub)");
            DestinationServer = destinationServer;

            string destinationUsername = null;
            Argument<string> destinationUsernameArg = syntax.DefineOption(
                "destination-username",
                ref destinationUsername,
                "Username for the Docker Registry the images are pushed to");
            DestinationUsername = destinationUsername;

            string sourcePassword = null;
            Argument<string> sourcePasswordArg = syntax.DefineOption(
                "source-password",
                ref sourcePassword,
                "Password for the Docker Registry the images are pulled from");
            SourcePassword = sourcePassword;

            string sourceServer = null;
            syntax.DefineOption(
                "source-server",
                ref sourceServer,
                "Docker Registry server the images are pulled from (default is Docker Hub)");
            SourceServer = sourceServer;

            string sourceUsername = null;
            Argument<string> sourceUsernameArg = syntax.DefineOption(
                "source-username",
                ref sourceUsername,
                "Username for the Docker Registry the images are pulled from");
            SourceUsername = sourceUsername;

            string sourceRepo = null;
            syntax.DefineParameter("source-repo", ref sourceRepo, "Docker repository to pull images from");
            SourceRepo = sourceRepo;

            if (destinationPasswordArg.IsSpecified ^ destinationUsernameArg.IsSpecified)
            {
                Logger.WriteError($"error: `{destinationUsernameArg.Name}` and `{destinationPasswordArg.Name}` must both be specified.");
                Environment.Exit(1);
            }

            if (sourcePasswordArg.IsSpecified ^ sourceUsernameArg.IsSpecified)
            {
                Logger.WriteError($"error: `{sourceUsernameArg.Name}` and `{sourcePasswordArg.Name}` must both be specified.");
                Environment.Exit(1);
            }
        }
    }
}
