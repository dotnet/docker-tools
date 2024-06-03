// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyAcrImagesOptions : CopyImagesOptions
    {
        public string SourceRepoPrefix { get; set; } = string.Empty;

        public string SourceRegistry { get; set; } = string.Empty;

        public string? ImageInfoPath { get; set; }
    }

    public class CopyAcrImagesOptionsBuilder : CopyImagesOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption<string?>("image-info", nameof(CopyAcrImagesOptions.ImageInfoPath),
                            "Path to image info file")
                    });

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(CopyAcrImagesOptions.SourceRepoPrefix),
                            "Prefix of the source ACR repository to copy images from")
                    })
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(CopyAcrImagesOptions.SourceRegistry),
                            "The source ACR to copy images from")
                    });
    }
}
#nullable disable
