// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishImageInfoOptions : Options, IGitOptionsHost
    {
        protected override string CommandHelp => "Publishes a build's merged image info.";

        public GitOptions GitOptions { get; } = new GitOptions("dotnet", "versions", "master", "build-info/docker/image-info.json");

        public string SourceImageInfoFolderPath { get; set; }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            GitOptions.ParseCommandLine(syntax);

            string sourceImageInfoFolderPath = null;
            syntax.DefineParameter(
                "source-image-info-folder-path",
                ref sourceImageInfoFolderPath,
                "Path to the folder where one or more local image info files are stored and are to be merged into GitHub.");
            SourceImageInfoFolderPath = sourceImageInfoFolderPath;
        }
    }
}
