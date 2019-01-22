// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Model;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateBuildMatrixOptions : Options, IManifestFilterOptions
    {
        protected override string CommandHelp => "Generate the VSTS build matrix for building the images";
        protected override string CommandName => "generateBuildMatrix";

        public Architecture Architecture { get; set; }
        public OS OsType { get; set; }
        public string OsVersion { get; set; }
        public MatrixType MatrixType { get; set; }
        public IEnumerable<string> Paths { get; set; }

        public GenerateBuildMatrixOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            DefineManifestFilterOptions(syntax, this);

            MatrixType matrixType = MatrixType.Build;
            syntax.DefineOption(
                "type",
                ref matrixType,
                value => (MatrixType)Enum.Parse(typeof(MatrixType), value, true),
                "Type of matrix to generate - build (default), test");
            MatrixType = matrixType;
        }
    }
}
