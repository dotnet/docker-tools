// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestOptions : DockerRegistryOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

        public string ImageInfoPath { get; set; } = string.Empty;

        public PublishManifestOptions() : base()
        {
        }
    }

    public class PublishManifestOptionsBuilder : DockerRegistryOptionsBuilder
    {
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder =
            new ManifestFilterOptionsBuilder();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_manifestFilterOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_manifestFilterOptionsBuilder.GetCliArguments())
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(PublishImageInfoOptions.ImageInfoPath),
                            "Image info file path")
                    });
    }
}
#nullable disable
