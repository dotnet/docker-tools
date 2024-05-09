// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
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

        public string OwnedAcr { get; set; } = string.Empty;
    }

    public class GetStaleImagesOptionsBuilder : CliOptionsBuilder
    {
        private readonly GitOptionsBuilder _gitOptionsBuilder = GitOptionsBuilder.BuildWithDefaults();
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();
        private readonly SubscriptionOptionsBuilder _subscriptionOptionsBuilder = new();
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();
        private readonly BaseImageOverrideOptionsBuilder _baseImageOverrideOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                .._subscriptionOptionsBuilder.GetCliOptions(),
                .._manifestFilterOptionsBuilder.GetCliOptions(),
                .._gitOptionsBuilder.GetCliOptions(),
                .._registryCredentialsOptionsBuilder.GetCliOptions(),
                .._baseImageOverrideOptionsBuilder.GetCliOptions(),
                CreateOption<string?>("owned-acr", nameof(GetStaleImagesOptions.OwnedAcr),
                        "The name of the ACR to authenticate with"),
            ];

        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                .._subscriptionOptionsBuilder.GetCliArguments(),
                .._manifestFilterOptionsBuilder.GetCliArguments(),
                .._gitOptionsBuilder.GetCliArguments(),
                .._registryCredentialsOptionsBuilder.GetCliArguments(),
                new Argument<string>(nameof(GetStaleImagesOptions.VariableName),
                    "The Azure Pipeline variable name to assign the image paths to"),
            ];
    }
}
