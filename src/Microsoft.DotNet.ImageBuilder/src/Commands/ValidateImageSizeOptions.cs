// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ValidateImageSizeOptions : ImageSizeOptionsBase
    {
        protected override string CommandHelp => "Validates the size of the images against a baseline";

        public bool CheckNewOrOldImagesOnly { get; set; }

        public ValidateImageSizeOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            bool checkNewOrOldImagesOnly = false;
            syntax.DefineOption("new-old-only", ref checkNewOrOldImagesOnly, "Only validate whether new or old images exist compared with baseline");
            CheckNewOrOldImagesOnly = checkNewOrOldImagesOnly;

            base.ParseCommandLine(syntax);
        }
    }
}
