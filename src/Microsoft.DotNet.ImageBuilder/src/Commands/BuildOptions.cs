// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

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
                        new Option<bool>("--push", "Push built images to Docker registry")
                        {
                            Name = nameof(BuildOptions.IsPushEnabled)
                        },
                        new Option<bool>("--retry", "Retry building images upon failure")
                        {
                            Name = nameof(BuildOptions.IsRetryEnabled)
                        },
                        new Option<bool>("--skip-pulling", "Skip explicitly pulling the base images of the Dockerfiles")
                        {
                            Name = nameof(BuildOptions.IsSkipPullingEnabled)
                        },
                        new Option<string?>("--image-info-output-path", "Path to output image info")
                        {
                            Name = nameof(BuildOptions.ImageInfoOutputPath)
                        },
                        new Option<string?>("--image-info-source-path", "Path to source image info")
                        {
                            Name = nameof(BuildOptions.ImageInfoSourcePath)
                        },
                        new Option<string?>("--source-repo", "Repo URL of the Dockerfile sources")
                        {
                            Name = nameof(BuildOptions.SourceRepoUrl)
                        },
                        new Option<bool>("--no-cache", "Disables build cache feature")
                        {
                            Name = nameof(BuildOptions.NoCache)
                        }
                    });
    }
}
#nullable disable
