// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.DockerTools.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class GenerateReadmesOptions : GenerateArtifactsOptions
    {
        public string SourceRepoUrl { get; set; } = string.Empty;

        public string? SourceRepoBranch { get; set; }

        public GenerateReadmesOptions() : base()
        {
        }
    }

    public class GenerateReadmesOptionsBuilder : GenerateArtifactsOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(
                    new Option[]
                    {
                        CreateOption<string?>("source-branch", nameof(GenerateReadmesOptions.SourceRepoBranch),
                            "Repo branch of the Dockerfile sources (default is commit SHA)")
                    }
                );

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(GenerateReadmesOptions.SourceRepoUrl),
                            "Repo URL of the Dockerfile sources")
                    }
                );
    }
}
#nullable disable
