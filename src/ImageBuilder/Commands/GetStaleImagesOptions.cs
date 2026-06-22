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

        public string? RegistryOverride { get; set; }

        public string? SourceRepoPrefix { get; set; }

        public string? ImageInfoRegistryOverride { get; set; }

        public string? ImageInfoRepoPrefix { get; set; }

        private static readonly GitOptionsBuilder GitBuilder = GitOptionsBuilder.BuildForRepositoryOperations();

        private static readonly Argument<string> VariableNameArgument = new(nameof(VariableName))
        {
            Description = "The Azure Pipeline variable name to assign the image paths to"
        };

        private static readonly Option<string?> RegistryOverrideOption = new($"--{ManifestOptions.RegistryOverrideName}")
        {
            Description = "Alternative registry that overrides the registry defined in each subscription's manifest. " +
                "Used together with --source-repo-prefix to redirect external base image lookups to a mirror location."
        };

        private static readonly Option<string?> SourceRepoPrefixOption = new("--source-repo-prefix")
        {
            Description = "Repo prefix used to locate mirrored external base images in the overridden registry " +
                "(e.g. 'mirror/'). Combined with --registry-override, external FROM tags are resolved against " +
                "'<registry-override>/<source-repo-prefix><original-repo>:<tag>' instead of their public source."
        };

        private static readonly Option<string?> ImageInfoRegistryOverrideOption = new("--image-info-registry-override")
        {
            Description = "Alternative registry to pull the image-info OCI artifact from. Defaults to the registry " +
                "defined in each subscription's manifest."
        };

        private static readonly Option<string?> ImageInfoRepoPrefixOption = new("--image-info-repo-prefix")
        {
            Description = "Repo prefix used to locate image-info OCI artifacts in the image-info registry."
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..SubscriptionOptions.GetCliOptions(),
            ..FilterOptions.GetCliOptions(),
            ..GitBuilder.GetCliOptions(),
            ..CredentialsOptions.GetCliOptions(),
            ..BaseImageOverrideOptions.GetCliOptions(),
            RegistryOverrideOption,
            SourceRepoPrefixOption,
            ImageInfoRegistryOverrideOption,
            ImageInfoRepoPrefixOption,
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
            RegistryOverride = result.GetValue(RegistryOverrideOption);
            SourceRepoPrefix = result.GetValue(SourceRepoPrefixOption);
            ImageInfoRegistryOverride = result.GetValue(ImageInfoRegistryOverrideOption);
            ImageInfoRepoPrefix = result.GetValue(ImageInfoRepoPrefixOption);
            VariableName = result.GetValue(VariableNameArgument) ?? string.Empty;
        }
    }
}
