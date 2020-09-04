// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class GenerateArtifactsOptions : ManifestOptions
    {
        public bool AllowOptionalTemplates { get; set; }

        public bool Validate { get; set; }

        protected GenerateArtifactsOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            bool allowOptionalTemplates = false;
            syntax.DefineOption("optional-templates", ref allowOptionalTemplates, "Do not require templates");
            AllowOptionalTemplates = allowOptionalTemplates;

            bool validate = false;
            syntax.DefineOption("validate", ref validate, "Validates the generated artifacts and templates are in sync");
            Validate = validate;
        }
    }
}
