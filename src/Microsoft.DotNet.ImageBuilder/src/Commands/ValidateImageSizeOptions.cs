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

            ImageSizeValidationMode mode = ImageSizeValidationMode.All;
            syntax.DefineOption("mode", ref mode,
                value => (ImageSizeValidationMode)Enum.Parse(typeof(ImageSizeValidationMode), value, true),
                $"Mode of validation. {EnumHelper.GetHelpTextOptions(mode)}");
            Mode = mode;
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
