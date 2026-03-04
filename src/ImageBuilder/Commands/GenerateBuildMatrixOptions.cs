// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Configuration;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateBuildMatrixOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();
        public MatrixType MatrixType { get; set; }
        public IEnumerable<string> CustomBuildLegGroups { get; set; } = Enumerable.Empty<string>();
        public int ProductVersionComponents { get; set; }
        public string? ImageInfoPath { get; set; }
        public IEnumerable<string> DistinctMatrixOsVersions { get; set; } = Enumerable.Empty<string>();
        public BaseImageOverrideOptions BaseImageOverrideOptions { get; set; } = new();
        public string? SourceRepoPrefix { get; set; }
        public string? SourceRepoUrl { get; set; }
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();
        public bool TrimCachedImages { get; set; }
    }

    public class GenerateBuildMatrixOptionsBuilder : ManifestOptionsBuilder
    {
        private const MatrixType DefaultMatrixType = MatrixType.PlatformDependencyGraph;

        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();
        private readonly BaseImageOverrideOptionsBuilder _baseImageOverrideOptionsBuilder = new();
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                .._manifestFilterOptionsBuilder.GetCliOptions(),
                .._baseImageOverrideOptionsBuilder.GetCliOptions(),
                .._registryCredentialsOptionsBuilder.GetCliOptions(),
                CreateOption("type", nameof(GenerateBuildMatrixOptions.MatrixType),
                    $"Type of matrix to generate. {EnumHelper.GetHelpTextOptions(DefaultMatrixType)}", DefaultMatrixType),
                CreateMultiOption<string>("custom-build-leg-group", nameof(GenerateBuildMatrixOptions.CustomBuildLegGroups),
                    "Name of custom build leg group to use."),
                CreateOption("product-version-components", nameof(GenerateBuildMatrixOptions.ProductVersionComponents),
                    "Number of components of the product version considered to be significant", 2),
                CreateOption<string?>("image-info", nameof(GenerateBuildMatrixOptions.ImageInfoPath),
                    "Path to image info file"),
                CreateMultiOption<string>("distinct-matrix-os-version", nameof(GenerateBuildMatrixOptions.DistinctMatrixOsVersions),
                    "OS version to be contained in its own distinct matrix"),
                CreateOption<string?>("source-repo-prefix", nameof(GenerateBuildMatrixOptions.SourceRepoPrefix),
                    "Prefix to add to the external base image names when pulling them"),
                CreateOption<string?>("source-repo", nameof(BuildOptions.SourceRepoUrl),
                    "Repo URL of the Dockerfile sources"),
                CreateOption<bool>("trim-cached-images", nameof(GenerateBuildMatrixOptions.TrimCachedImages),
                    "Whether to trim cached images from the set of paths"),
            ];

        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                .._manifestFilterOptionsBuilder.GetCliArguments(),
                .._baseImageOverrideOptionsBuilder.GetCliArguments(),
                .._registryCredentialsOptionsBuilder.GetCliArguments()
            ];
    }
}
