// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class TrimCachedPlatformsOptions : Options
    {
        protected override string CommandHelp => "Trims platforms marked as cached from the image info file";

        public string ImageInfoPath { get; set; }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string imageInfoPath = null;
            syntax.DefineParameter("image-info", ref imageInfoPath, "Path to image info file");
            ImageInfoPath = imageInfoPath;
        }
    }
}
