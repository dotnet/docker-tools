// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateBuildMatrixOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();
        public MatrixType MatrixType { get; set; }
        public IEnumerable<string> CustomBuildLegGroups { get; set; } = Enumerable.Empty<string>();
        public int ProductVersionComponents { get; set; }
        public string? ImageInfoPath { get; set; }

        public GenerateBuildMatrixOptions() : base()
        {
        }
    }

    public class GenerateBuildMatrixSymbolsBuilder : ManifestSymbolsBuilder
    {
        private const MatrixType DefaultMatrixType = MatrixType.PlatformDependencyGraph;

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(ManifestFilterOptions.GetCliOptions())
                .Concat(
                    new Option[]
                    {
                        new Option<MatrixType>("--type", () => DefaultMatrixType,
                            $"Type of matrix to generate. {EnumHelper.GetHelpTextOptions(DefaultMatrixType)}")
                        {
                            Name = nameof(MatrixType)
                        },
                        new Option<string[]>("--custom-build-leg-group", () => Array.Empty<string>(), "Name of custom build leg group to use.")
                        {
                            Name = nameof(GenerateBuildMatrixOptions.CustomBuildLegGroups)
                        },
                        new Option<int>("--product-version-components", () => 2, "Number of components of the product version considered to be significant")
                        {
                            Name = nameof(GenerateBuildMatrixOptions.ProductVersionComponents)
                        },
                        new Option<string?>("--image-info", "Path to image info file")
                        {
                            Name = nameof(GenerateBuildMatrixOptions.ImageInfoPath)
                        }
                    });
    }
}
#nullable disable
