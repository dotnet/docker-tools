// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateReadmesOptions : GenerateArtifactsOptions
    {
        public string SourceRepoUrl { get; set; } = string.Empty;

        public string? SourceRepoBranch { get; set; }

        private static readonly Option<string?> SourceBranchOption = new(CliHelper.FormatAlias("source-branch"))
        {
            Description = "Repo branch of the Dockerfile sources (default is commit SHA)"
        };

        private static readonly Argument<string> SourceRepoUrlArgument = new(nameof(SourceRepoUrl))
        {
            Description = "Repo URL of the Dockerfile sources"
        };

        public override IEnumerable<Option> GetCliOptions() =>
            [..base.GetCliOptions(), SourceBranchOption];

        public override IEnumerable<Argument> GetCliArguments() =>
            [..base.GetCliArguments(), SourceRepoUrlArgument];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            SourceRepoBranch = result.GetValue(SourceBranchOption);
            SourceRepoUrl = result.GetValue(SourceRepoUrlArgument) ?? string.Empty;
        }
    }
}
