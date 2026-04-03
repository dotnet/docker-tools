// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class GenerateArtifactsOptions : ManifestOptions
    {
        public bool AllowOptionalTemplates { get; set; }

        public bool Validate { get; set; }

        private static readonly Option<bool> OptionalTemplatesOption = new(CliHelper.FormatAlias("optional-templates"))
        {
            Description = "Do not require templates"
        };

        private static readonly Option<bool> ValidateOption = new(CliHelper.FormatAlias("validate"))
        {
            Description = "Validates the generated artifacts and templates are in sync"
        };

        public override IEnumerable<Option> GetCliOptions() =>
            [..base.GetCliOptions(), OptionalTemplatesOption, ValidateOption];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            AllowOptionalTemplates = result.GetValue(OptionalTemplatesOption);
            Validate = result.GetValue(ValidateOption);
        }
    }
}
