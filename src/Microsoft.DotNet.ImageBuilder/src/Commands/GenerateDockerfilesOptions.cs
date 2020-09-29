// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateDockerfilesOptions : GenerateArtifactsOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        protected override string CommandHelp => "Generates the Dockerfiles from Cottle based templates (http://r3c.github.io/cottle/)";

        public Architecture? ArchTagSuffixExclusion { get; set; }

        public GenerateDockerfilesOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);

            Architecture? archTagSuffixExclusion = null;
            syntax.DefineOption("arch-tag-suffix-exclusion", ref archTagSuffixExclusion,
                value => (Architecture)Enum.Parse(typeof(Architecture), value, true),
                "Architecture that should be excluded as a tag suffix");
            ArchTagSuffixExclusion = archTagSuffixExclusion;
        }
    }
}
