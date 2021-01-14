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
    public class BuildOptions : DockerRegistryOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

        public bool IsPushEnabled { get; set; }
        public bool IsRetryEnabled { get; set; }
        public bool IsSkipPullingEnabled { get; set; }
        public string? ImageInfoOutputPath { get; set; }
        public string? ImageInfoSourcePath { get; set; }
        public string? SourceRepoUrl { get; set; }
        public bool NoCache { get; set; }
    }

    public class BuildSymbolsBuilder : DockerRegistrySymbolsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(ManifestFilterOptions.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        CreateOption<bool>("push", nameof(BuildOptions.IsPushEnabled),
                            "Push built images to Docker registry"),
                        CreateOption<bool>("retry", nameof(BuildOptions.IsRetryEnabled),
                            "Retry building images upon failure"),
                        CreateOption<bool>("skip-pulling", nameof(BuildOptions.IsSkipPullingEnabled),
                            "Skip explicitly pulling the base images of the Dockerfiles"),
                        CreateOption<string?>("image-info-output-path", nameof(BuildOptions.ImageInfoOutputPath),
                            "Path to output image info"),
                        CreateOption<string?>("--image-info-source-path", nameof(BuildOptions.ImageInfoSourcePath),
                            "Path to source image info"),
                        CreateOption<string?>("--source-repo", nameof(BuildOptions.SourceRepoUrl),
                            "Repo URL of the Dockerfile sources"),
                        CreateOption<bool>("--no-cache", nameof(BuildOptions.NoCache),
                            "Disables build cache feature")
                    });
    }
}
#nullable disable
