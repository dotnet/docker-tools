// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishImageInfoOptions : Options, IGitOptionsHost
    {
        protected override string CommandHelp => "Publishes a build's merged image info.";

        public GitOptions GitOptions { get; } = GitOptions.GetVersionsRepoImageInfoOptions();

        public string ImageInfoPath { get; set; }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            GitOptions.DefineOptions(syntax);
        }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            GitOptions.DefineParameters(syntax);

            string imageInfoPath = null;
            syntax.DefineParameter(
                "image-info-path",
                ref imageInfoPath,
                "Image info file path");
            ImageInfoPath = imageInfoPath;
        }
    }
}
