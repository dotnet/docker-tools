// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class QueueBuildOptions : Options
    {
        public string SubscriptionsPath { get; set; } = string.Empty;
        public AzdoOptions AzdoOptions { get; set; } = new();
        public IEnumerable<string> AllSubscriptionImagePaths { get; set; } = Enumerable.Empty<string>();
        public GitOptions GitOptions { get; set; } = new();

        private static readonly GitOptionsBuilder GitBuilder =
            GitOptionsBuilder.Build()
                .WithGitHubAuth(description: "Auth token to use to connect to GitHub for posting notifications")
                .WithOwner(description: "Owner of the GitHub repo to post notifications to")
                .WithRepo(description: "Name of the GitHub repo to post notifications to");

        private const string DefaultSubscriptionsPath = "subscriptions.json";

        private static readonly Option<string> SubscriptionsPathOption = new(CliHelper.FormatAlias("subscriptions-path"))
        {
            Description = $"Path to the subscriptions file (defaults to '{DefaultSubscriptionsPath}').",
            DefaultValueFactory = _ => DefaultSubscriptionsPath
        };

        private static readonly Option<string[]> ImagePathsOption = new(CliHelper.FormatAlias("image-paths"))
        {
            Description = "JSON string mapping a subscription ID to the image paths to be built (from the output variable of getStaleImages)",
            DefaultValueFactory = _ => Array.Empty<string>(),
            AllowMultipleArgumentsPerToken = false
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..AzdoOptions.GetCliOptions(),
            SubscriptionsPathOption,
            ImagePathsOption,
            ..GitBuilder.GetCliOptions(),
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
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
            AzdoOptions.Bind(result);
            SubscriptionsPath = result.GetValue(SubscriptionsPathOption) ?? string.Empty;
            AllSubscriptionImagePaths = result.GetValue(ImagePathsOption) ?? [];
            GitBuilder.Bind(result, GitOptions);
        }
    }
}
