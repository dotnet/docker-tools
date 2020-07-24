// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateBuildMatrixOptions : ManifestOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Generate the Azure DevOps build matrix for building the images";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();
        public MatrixType MatrixType { get; set; }
        public string CustomBuildLegGroup { get; set; }
        public int ProductVersionComponents { get; set; }

        public GenerateBuildMatrixOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);

            MatrixType matrixType = MatrixType.PlatformDependencyGraph;
            syntax.DefineOption(
                "type",
                ref matrixType,
                value => (MatrixType)Enum.Parse(typeof(MatrixType), value, true),
                $"Type of matrix to generate. {EnumHelper.GetHelpTextOptions(matrixType)}");
            MatrixType = matrixType;

            string customBuildLegGroup = null;
            syntax.DefineOption(
                "custom-build-leg-group",
                ref customBuildLegGroup,
                "Name of custom build leg group to use.");
            CustomBuildLegGroup = customBuildLegGroup;

            int productVersionComponents = 2;
            syntax.DefineOption(
                "product-version-components",
                ref productVersionComponents,
                "Number of components of the product version considered to be significant");
            ProductVersionComponents = productVersionComponents;
        }
    }
}
