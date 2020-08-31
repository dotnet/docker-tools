// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateImageGraphOptions : ManifestOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Generate a DOT (graph description language) file illustrating the image and layer hierarchy";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public string OutputPath { get; set; }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);
        }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string outputPath = null;
            syntax.DefineParameter("output-path", ref outputPath, "The path to write the graph to");
            OutputPath = outputPath;
        }
    }
}
