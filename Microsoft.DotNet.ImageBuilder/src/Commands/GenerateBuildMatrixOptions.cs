// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateBuildMatrixOptions : Options, IFilterableOptions
    {
        protected override string CommandHelp => "Generate the VSTS build matrix for building the images";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();
        public MatrixType MatrixType { get; set; }

        public GenerateBuildMatrixOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            FilterOptions.ParseCommandLine(syntax);

            MatrixType matrixType = MatrixType.PlatformDependencyGraph;
            syntax.DefineOption(
                "type",
                ref matrixType,
                value => (MatrixType)Enum.Parse(typeof(MatrixType), value, true),
                "Type of matrix to generate - build (default), test");
            MatrixType = matrixType;
        }
    }
}
