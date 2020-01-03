// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateImageSizeBaselineOptions : ImageSizeOptions
    {
        protected override string CommandHelp => "Updates an image size baseline file with current image sizes";

        public bool AllBaselineData { get; set; }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            bool allBaselineData = false;
            syntax.DefineOption("all", ref allBaselineData, "Updates baseline for all images regardless of size variance");
            AllBaselineData = allBaselineData;

            base.ParseCommandLine(syntax);
        }
    }
}
