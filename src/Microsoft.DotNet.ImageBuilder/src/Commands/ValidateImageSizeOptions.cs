// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ValidateImageSizeOptions : ImageSizeOptions
    {
        protected override string CommandHelp => "Validates the size of the images against a baseline";

        public ImageSizeValidationMode Mode { get; set; }

        public ValidateImageSizeOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            string mode = ImageSizeValidationMode.All.ToString().ToLowerInvariant();
            syntax.DefineOption("mode", ref mode, "Mode of validation. Options: all (default), size, integrity");
            Mode = (ImageSizeValidationMode)Enum.Parse(typeof(ImageSizeValidationMode), mode, ignoreCase: true);
        }
    }

    [Flags]
    public enum ImageSizeValidationMode
    {
        All = Size | Integrity,
        Size = 1,
        Integrity = 2
    }
}
