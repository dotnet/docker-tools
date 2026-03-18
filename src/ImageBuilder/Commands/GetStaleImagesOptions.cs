// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetStaleImagesOptions : Options, IFilterableOptions, IGitOptionsHost
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

        public GitOptions GitOptions { get; set; } = new GitOptions();

        public SubscriptionOptions SubscriptionOptions { get; set; } = new SubscriptionOptions();

        public string VariableName { get; set; } = string.Empty;

        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new RegistryCredentialsOptions();

        public BaseImageOverrideOptions BaseImageOverrideOptions { get; set; } = new();

        private static readonly GitOptionsBuilder GitBuilder = GitOptionsBuilder.BuildWithDefaults();

        private static readonly Argument<string> VariableNameArgument = new(nameof(VariableName))
        {
            Description = "The Azure Pipeline variable name to assign the image paths to"
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..SubscriptionOptions.GetCliOptions(),
            ..FilterOptions.GetCliOptions(),
            ..GitBuilder.GetCliOptions(),
            ..CredentialsOptions.GetCliOptions(),
            ..BaseImageOverrideOptions.GetCliOptions(),
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..SubscriptionOptions.GetCliArguments(),
            ..FilterOptions.GetCliArguments(),
            ..GitBuilder.GetCliArguments(),
            ..CredentialsOptions.GetCliArguments(),
            VariableNameArgument,
        ];

        public override IEnumerable<Action<CommandResult>> GetValidators() =>
        [
            ..base.GetValidators(),
            ..GitBuilder.GetValidators(),
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            SubscriptionOptions.Bind(result);
            FilterOptions.Bind(result);
            GitBuilder.Bind(result, GitOptions);
            CredentialsOptions.Bind(result);
            BaseImageOverrideOptions.Bind(result);
            VariableName = result.GetValue(VariableNameArgument) ?? string.Empty;
        }
    }
}
