// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishMcrDocsOptions : ManifestOptions, IGitOptionsHost
    {
        public GitOptions GitOptions { get; set; } = new();

        public string SourceRepoUrl { get; set; } = string.Empty;

        public bool ExcludeProductFamilyReadme { get; set; }

        public string? RootPath { get; set; }

        public PublishMcrDocsOptions() : base()
        {
        }
    }

    public class PublishMcrDocsOptionsBuilder : ManifestOptionsBuilder
    {
        private readonly GitOptionsBuilder _gitOptionsBuilder = GitOptionsBuilder.BuildWithDefaults();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_gitOptionsBuilder.GetCliOptions())
                .Concat(
                    [
                        CreateOption<bool>("exclude-product-family", nameof(PublishMcrDocsOptions.ExcludeProductFamilyReadme),
                            "Excludes the product family readme from being published"),
                        CreateOption<string>("root", nameof(PublishMcrDocsOptions.RootPath),
                            "Root path from which to copy readmes"),
                    ]);

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_gitOptionsBuilder.GetCliArguments())
                .Concat(
                    [
                        new Argument<string>(nameof(PublishMcrDocsOptions.SourceRepoUrl),
                            "Repo URL of the Dockerfile sources")
                    ]
                );

        public override IEnumerable<ValidateSymbol<CommandResult>> GetValidators() =>
        [
            ..base.GetValidators(),
            .._gitOptionsBuilder.GetValidators()
        ];
    }
}
