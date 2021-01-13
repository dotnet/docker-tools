// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

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

    public class ValidateImageSizeSymbolsBuilder : ImageSizeSymbolsBuilder
    {
        private const ImageSizeValidationMode DefaultValidationMode = ImageSizeValidationMode.All;

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        new Option<ImageSizeValidationMode>("--mode", () => DefaultValidationMode,
                            $"Mode of validation. {EnumHelper.GetHelpTextOptions(DefaultValidationMode)}")
                        {
                            Name = nameof(ValidateImageSizeOptions.Mode)
                        }
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
