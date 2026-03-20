// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

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

    public abstract class GenerateArtifactsOptionsBuilder : ManifestOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption<bool>("optional-templates", nameof(GenerateArtifactsOptions.AllowOptionalTemplates),
                            "Do not require templates"),
                        CreateOption<bool>("validate", nameof(GenerateArtifactsOptions.Validate),
                            "Validates the generated artifacts and templates are in sync")
                    }
                );
    }
}
