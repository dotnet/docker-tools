// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PostPublishNotificationOptions : ManifestOptions
    {
        public IList<string> TaskNames { get; set; } = new List<string>();
        public string SourceRepo { get; set; } = string.Empty;
        public string SourceBranch { get; set; } = string.Empty;
        public string ImageInfoPath { get; set; } = string.Empty;
        public int BuildId { get; set; }
        public GitOptions GitOptions { get; set; } = new();
        public AzdoOptions AzdoOptions { get; set; } = new();

        private static readonly GitOptionsBuilder GitBuilder =
            GitOptionsBuilder.Build()
                .WithGitHubAuth(
                    isRequired: true,
                    description: "Auth token to use to connect to GitHub for posting notifications")
                .WithOwner(isRequired: true, description: "Owner of the GitHub repo to post notifications to")
                .WithRepo(isRequired: true, description: "Name of the GitHub repo to post notifications to");

        private static readonly Option<string[]> TaskNamesOption = new(CliHelper.FormatAlias("task"))
        {
            Description = "Name of a build task to report the result of",
            DefaultValueFactory = _ => Array.Empty<string>(),
            AllowMultipleArgumentsPerToken = false
        };

        private static readonly Argument<string> SourceRepoArgument = new(nameof(SourceRepo))
        {
            Description = "Name of the repo that is the source of the publish"
        };

        private static readonly Argument<string> SourceBranchArgument = new(nameof(SourceBranch))
        {
            Description = "Name of the repo branch that is the source of the publish"
        };

        private static readonly Argument<string> ImageInfoPathArgument = new(nameof(ImageInfoPath))
        {
            Description = "Path to image info file"
        };

        private static readonly Argument<int> BuildIdArgument = new(nameof(BuildId))
        {
            Description = "ID of the build that executed the publish"
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            TaskNamesOption,
            ..AzdoOptions.GetCliOptions(),
            ..GitBuilder.GetCliOptions(),
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            SourceRepoArgument,
            SourceBranchArgument,
            ImageInfoPathArgument,
            BuildIdArgument,
            ..AzdoOptions.GetCliArguments(),
            ..GitBuilder.GetCliArguments(),
        ];

        public override IEnumerable<Action<CommandResult>> GetValidators() =>
        [
            ..base.GetValidators(),
            ..GitBuilder.GetValidators(),
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            TaskNames = result.GetValue(TaskNamesOption) ?? [];
            SourceRepo = result.GetValue(SourceRepoArgument) ?? string.Empty;
            SourceBranch = result.GetValue(SourceBranchArgument) ?? string.Empty;
            ImageInfoPath = result.GetValue(ImageInfoPathArgument) ?? string.Empty;
            BuildId = result.GetValue(BuildIdArgument);
            AzdoOptions.Bind(result);
            GitBuilder.Bind(result, GitOptions);
        }
    }
}
