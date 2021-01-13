// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateImageSizeBaselineOptions : ImageSizeOptions
    {
        public bool AllBaselineData { get; set; }
    }

    public class UpdateImageSizeBaselineSymbolsBuilder : ImageSizeSymbolsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        new Option<bool>("--all", "Updates baseline for all images regardless of size variance")
                        {
                            Name = nameof(UpdateImageSizeBaselineOptions.AllBaselineData)
                        }
                    });
    }
}
#nullable disable

