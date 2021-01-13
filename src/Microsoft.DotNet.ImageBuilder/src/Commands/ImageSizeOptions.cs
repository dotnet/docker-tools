// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ImageSizeOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public int AllowedVariance { get; set; }
        public string BaselinePath { get; set; }
        public bool IsPullEnabled { get; set; }
    }

    public abstract class ImageSizeSymbolsBuilder : ManifestSymbolsBuilder
    {
        private const int AllowedVarianceDefault = 5;

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(ManifestFilterOptions.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        new Option<int>("--variance", () => AllowedVarianceDefault,
                            $"Allowed percent variance in size (default is `{AllowedVarianceDefault}`")
                        {
                            Name = nameof(ImageSizeOptions.AllowedVariance)
                        },
                        new Option<bool>("--pull", "Pull the images vs using local images")
                        {
                            Name = nameof(ImageSizeOptions.IsPullEnabled)
                        }
                    }
                );

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(ImageSizeOptions.BaselinePath), "Path to the baseline file")
                    }
                );
    }
}
