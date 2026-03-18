// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Configuration;

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

        private const MatrixType DefaultMatrixType = MatrixType.PlatformDependencyGraph;

        private static readonly Option<MatrixType> MatrixTypeOption = new(CliHelper.FormatAlias("type"))
        {
            Description = $"Type of matrix to generate. {EnumHelper.GetHelpTextOptions(DefaultMatrixType)}",
            DefaultValueFactory = _ => DefaultMatrixType
        };

        private static readonly Option<string[]> CustomBuildLegGroupsOption = new(CliHelper.FormatAlias("custom-build-leg-group"))
        {
            Description = "Name of custom build leg group to use.",
            DefaultValueFactory = _ => Array.Empty<string>(),
            AllowMultipleArgumentsPerToken = false
        };

        private static readonly Option<int> ProductVersionComponentsOption = new(CliHelper.FormatAlias("product-version-components"))
        {
            Description = "Number of components of the product version considered to be significant",
            DefaultValueFactory = _ => 2
        };

        private static readonly Option<string?> ImageInfoOption = new(CliHelper.FormatAlias("image-info"))
        {
            Description = "Path to image info file"
        };

        private static readonly Option<string[]> DistinctMatrixOsVersionsOption = new(CliHelper.FormatAlias("distinct-matrix-os-version"))
        {
            Description = "OS version to be contained in its own distinct matrix",
            DefaultValueFactory = _ => Array.Empty<string>(),
            AllowMultipleArgumentsPerToken = false
        };

        private static readonly Option<string?> SourceRepoPrefixOption = new(CliHelper.FormatAlias("source-repo-prefix"))
        {
            Description = "Prefix to add to the external base image names when pulling them"
        };

        private static readonly Option<string?> SourceRepoOption = new(CliHelper.FormatAlias("source-repo"))
        {
            Description = "Repo URL of the Dockerfile sources"
        };

        private static readonly Option<bool> TrimCachedImagesOption = new(CliHelper.FormatAlias("trim-cached-images"))
        {
            Description = "Whether to trim cached images from the set of paths"
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..FilterOptions.GetCliOptions(),
            ..BaseImageOverrideOptions.GetCliOptions(),
            ..CredentialsOptions.GetCliOptions(),
            MatrixTypeOption,
            CustomBuildLegGroupsOption,
            ProductVersionComponentsOption,
            ImageInfoOption,
            DistinctMatrixOsVersionsOption,
            SourceRepoPrefixOption,
            SourceRepoOption,
            TrimCachedImagesOption,
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..FilterOptions.GetCliArguments(),
            ..BaseImageOverrideOptions.GetCliArguments(),
            ..CredentialsOptions.GetCliArguments(),
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            FilterOptions.Bind(result);
            BaseImageOverrideOptions.Bind(result);
            CredentialsOptions.Bind(result);
            MatrixType = result.GetValue(MatrixTypeOption);
            CustomBuildLegGroups = result.GetValue(CustomBuildLegGroupsOption) ?? [];
            ProductVersionComponents = result.GetValue(ProductVersionComponentsOption);
            ImageInfoPath = result.GetValue(ImageInfoOption);
            DistinctMatrixOsVersions = result.GetValue(DistinctMatrixOsVersionsOption) ?? [];
            SourceRepoPrefix = result.GetValue(SourceRepoPrefixOption);
            SourceRepoUrl = result.GetValue(SourceRepoOption);
            TrimCachedImages = result.GetValue(TrimCachedImagesOption);
        }
    }
}
