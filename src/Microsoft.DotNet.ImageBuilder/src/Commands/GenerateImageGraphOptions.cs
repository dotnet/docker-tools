// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateImageGraphOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public string OutputPath { get; set; } = string.Empty;
    }

    public class GenerateImageGraphOptionsBuilder : ManifestOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions().Concat(ManifestFilterOptions.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
            .Concat(
                new Argument[]
                {
                    new Argument<string>(nameof(GenerateImageGraphOptions.OutputPath),
                        "The path to write the graph to")
                });
    }
}
#nullable disable
