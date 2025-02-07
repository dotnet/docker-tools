// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ValidateImageSizeOptions : ImageSizeOptions
    {
        public ImageSizeValidationMode Mode { get; set; }

        public ValidateImageSizeOptions() : base()
        {
        }
    }

    public class ValidateImageSizeOptionsBuilder : ImageSizeOptionsBuilder
    {
        private const ImageSizeValidationMode DefaultValidationMode = ImageSizeValidationMode.All;

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption("mode", nameof(ValidateImageSizeOptions.Mode),
                            $"Mode of validation. {EnumHelper.GetHelpTextOptions(DefaultValidationMode)}", DefaultValidationMode)
                    }
                );
    }

    [Flags]
    public enum ImageSizeValidationMode
    {
        All = Size | Integrity,
        Size = 1,
        Integrity = 2
    }
}
#nullable disable
