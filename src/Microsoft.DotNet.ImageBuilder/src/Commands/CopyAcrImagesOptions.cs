// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyAcrImagesOptions : CopyImagesOptions
    {
        public string SourceRepoPrefix { get; set; } = string.Empty;

        public string? ImageInfoPath { get; set; }
    }

    public class CopyAcrImagesSymbolsBuilder : CopyImagesSymbolsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        new Option<string?>("--image-info", "Path to image info file")
                        {
                            Name = nameof(CopyAcrImagesOptions.ImageInfoPath)
                        }
                    });

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(CopyAcrImagesOptions.SourceRepoPrefix),
                            "Prefix of the source ACR repository to copy images from")
                    });
    }
}
#nullable disable
