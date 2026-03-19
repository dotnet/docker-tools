// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PullImagesOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new();

        public string? OutputVariableName { get; set; }
        public string? ImageInfoPath { get; set; }

        private static readonly Option<string?> OutputVarOption = new(CliHelper.FormatAlias("output-var"))
        {
            Description = "Azure DevOps variable name to use for outputting the list of pulled image tags"
        };

        private static readonly Option<string?> ImageInfoOption = new(CliHelper.FormatAlias("image-info"))
        {
            Description = "Path to the image info file describing which images are to be pulled"
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..FilterOptions.GetCliOptions(),
            OutputVarOption,
            ImageInfoOption,
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..FilterOptions.GetCliArguments(),
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            FilterOptions.Bind(result);
            OutputVariableName = result.GetValue(OutputVarOption);
            ImageInfoPath = result.GetValue(ImageInfoOption);
        }
    }
}
