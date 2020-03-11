// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class MergeImageInfoOptions : ManifestOptions
    {
        protected override string CommandHelp => "Merges the content of multiple image info files into one file";

        public string SourceImageInfoFolderPath { get; set; }

        public string DestinationImageInfoPath { get; set; }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string sourceImageInfoFolderPath = null;
            syntax.DefineParameter(
                "source-path",
                ref sourceImageInfoFolderPath,
                "Folder path containing image info files");
            SourceImageInfoFolderPath = sourceImageInfoFolderPath;

            string destinationPath = null;
            syntax.DefineParameter(
                "destination-path",
                ref destinationPath,
                "Path to store the merged image info content");
            DestinationImageInfoPath = destinationPath;
        }
    }
}
