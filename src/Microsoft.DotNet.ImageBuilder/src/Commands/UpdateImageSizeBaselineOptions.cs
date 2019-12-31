// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateImageSizeBaselineOptions : ImageSizeOptionsBase
    {
        protected override string CommandHelp => "Updates an image size baseline file with current image sizes";

        public bool OutOfRangeOnly { get; set; }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            bool outOfRangeOnly = false;
            syntax.DefineOption("out-of-range-only", ref outOfRangeOnly, "Only update baseline for image sizes outside the allowed range");
            OutOfRangeOnly = outOfRangeOnly;

            base.ParseCommandLine(syntax);
        }
    }
}
