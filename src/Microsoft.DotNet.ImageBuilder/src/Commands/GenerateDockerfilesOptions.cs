// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateDockerfilesOptions : ManifestOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Generates the Dockerfiles from Cottle based templates (http://r3c.github.io/cottle/)";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public bool Validate { get; set; }

        public GenerateDockerfilesOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);

            bool validate = false;
            syntax.DefineOption("validate", ref validate, "Validates the Dockerfiles and templates are in sync");
            Validate = validate;
        }
    }
}
