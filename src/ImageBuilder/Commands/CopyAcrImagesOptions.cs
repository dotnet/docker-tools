// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyAcrImagesOptions : CopyImagesOptions
    {
        public string SourceRepoPrefix { get; set; } = string.Empty;

        public string SourceRegistry { get; set; } = string.Empty;

        public string? ImageInfoPath { get; set; }

        private static readonly Option<string?> ImageInfoOption = new(CliHelper.FormatAlias("image-info"))
        {
            Description = "Path to image info file"
        };

        private static readonly Argument<string> SourceRepoPrefixArgument = new(nameof(SourceRepoPrefix))
        {
            Description = "Prefix of the source ACR repository to copy images from"
        };

        private static readonly Argument<string> SourceRegistryArgument = new(nameof(SourceRegistry))
        {
            Description = "The source ACR to copy images from"
        };

        public override IEnumerable<Option> GetCliOptions() =>
            [..base.GetCliOptions(), ImageInfoOption];

        public override IEnumerable<Argument> GetCliArguments() =>
            [..base.GetCliArguments(), SourceRepoPrefixArgument, SourceRegistryArgument];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            ImageInfoPath = result.GetValue(ImageInfoOption);
            SourceRepoPrefix = result.GetValue(SourceRepoPrefixArgument) ?? string.Empty;
            SourceRegistry = result.GetValue(SourceRegistryArgument) ?? string.Empty;
        }
    }
}
