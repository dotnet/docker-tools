﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.DockerTools.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class PullImagesOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new();

        public string? OutputVariableName { get; set; }
        public string? ImageInfoPath { get; set; }
    }

    public class PullImagesOptionsBuilder : ManifestOptionsBuilder
    {
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_manifestFilterOptionsBuilder.GetCliOptions())
                .Concat(new Option[]
                {
                    CreateOption<string>("output-var", nameof(PullImagesOptions.OutputVariableName),
                        "Azure DevOps variable name to use for outputting the list of pulled image tags"),
                    CreateOption<string>("image-info", nameof(PullImagesOptions.ImageInfoPath),
                        "Path to the image info file describing which images are to be pulled")
                });

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_manifestFilterOptionsBuilder.GetCliArguments());
    }
}
#nullable disable
