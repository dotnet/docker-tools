// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class GenerateArtifactsOptions : ManifestOptions
    {
        public bool AllowOptionalTemplates { get; set; }

        public bool Validate { get; set; }

        protected GenerateArtifactsOptions() : base()
        {
        }
    }

    public abstract class GenerateArtifactsSymbolsBuilder : ManifestSymbolsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        new Option<bool>("--optional-templates", "Do not require templates")
                        {
                            Name = nameof(GenerateArtifactsOptions.AllowOptionalTemplates)
                        },
                        new Option<bool>("--validate", "Validates the generated artifacts and templates are in sync")
                        {
                            Name = nameof(GenerateArtifactsOptions.Validate)
                        }
                    }
                );
    }
}
#nullable disable
