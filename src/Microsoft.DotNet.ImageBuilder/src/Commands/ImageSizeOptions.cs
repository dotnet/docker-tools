// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ImageSizeOptions : ManifestOptions, IFilterableOptions, IDockerCredsOptionsHost
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

        public DockerCredsOptions DockerCredsOptions { get; set; } = new DockerCredsOptions();

        public int AllowedVariance { get; set; }
        public string BaselinePath { get; set; }
        public bool IsPullEnabled { get; set; }
    }

    public abstract class ImageSizeOptionsBuilder : ManifestOptionsBuilder
    {
        private const int AllowedVarianceDefault = 5;

        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder =
            new ManifestFilterOptionsBuilder();

        private readonly DockerCredsOptionsBuilder _dockerCredsOptionsBuilder =
            new DockerCredsOptionsBuilder();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_manifestFilterOptionsBuilder.GetCliOptions())
                .Concat(_dockerCredsOptionsBuilder.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        CreateOption("variance", nameof(ImageSizeOptions.AllowedVariance),
                            $"Allowed percent variance in size (default is `{AllowedVarianceDefault}`", AllowedVarianceDefault),
                        CreateOption<bool>("pull", nameof(ImageSizeOptions.IsPullEnabled),
                            "Pull the images vs using local images")
                    }
                );

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_manifestFilterOptionsBuilder.GetCliArguments())
                .Concat(_dockerCredsOptionsBuilder.GetCliArguments())
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(ImageSizeOptions.BaselinePath), "Path to the baseline file")
                    }
                );
    }
}
