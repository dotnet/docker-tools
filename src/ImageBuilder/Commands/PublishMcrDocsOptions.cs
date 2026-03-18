// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishMcrDocsOptions : ManifestOptions, IGitOptionsHost
    {
        public GitOptions GitOptions { get; set; } = new();

        public string SourceRepoUrl { get; set; } = string.Empty;

        public bool ExcludeProductFamilyReadme { get; set; }

        public string? RootPath { get; set; }

        private static readonly GitOptionsBuilder GitBuilder = GitOptionsBuilder.BuildWithDefaults();

        private static readonly Option<bool> ExcludeProductFamilyOption = new(CliHelper.FormatAlias("exclude-product-family"))
        {
            Description = "Excludes the product family readme from being published"
        };

        private static readonly Option<string?> RootPathOption = new(CliHelper.FormatAlias("root"))
        {
            Description = "Root path from which to copy readmes"
        };

        private static readonly Argument<string> SourceRepoUrlArgument = new(nameof(SourceRepoUrl))
        {
            Description = "Repo URL of the Dockerfile sources"
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..GitBuilder.GetCliOptions(),
            ExcludeProductFamilyOption,
            RootPathOption,
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..GitBuilder.GetCliArguments(),
            SourceRepoUrlArgument,
        ];

        public override IEnumerable<Action<CommandResult>> GetValidators() =>
        [
            ..base.GetValidators(),
            ..GitBuilder.GetValidators(),
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            GitBuilder.Bind(result, GitOptions);
            SourceRepoUrl = result.GetValue(SourceRepoUrlArgument) ?? string.Empty;
            ExcludeProductFamilyReadme = result.GetValue(ExcludeProductFamilyOption);
            RootPath = result.GetValue(RootPathOption);
        }
    }
}
