// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new();
        public BaseImageOverrideOptions BaseImageOverrideOptions { get; set; } = new();
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();
        public ServiceConnection? StorageServiceConnection { get; set; }

        public bool IsPushEnabled { get; set; }
        public bool IsRetryEnabled { get; set; }
        public bool IsSkipPullingEnabled { get; set; }
        public string? ImageInfoOutputPath { get; set; }
        public string? ImageInfoSourcePath { get; set; }
        public string? SourceRepoUrl { get; set; }
        public bool NoCache { get; set; }
        public string? SourceRepoPrefix { get; set; }
        public IDictionary<string, string> BuildArgs { get; set; } = new Dictionary<string, string>();
        public string[] DockerBuildOptions { get; set; } = [];
        public bool SkipPlatformCheck { get; set; }
        public string? OutputVariableName { get; set; }
        public bool Internal { get; set; }

        private static readonly ServiceConnectionOptionsBuilder ServiceConnectionBuilder = new();

        private static readonly Option<ServiceConnection?> StorageServiceConnectionOption =
            ServiceConnectionBuilder.GetCliOption(
                "storage-service-connection",
                "Storage account to use for internal builds.");

        private static readonly Option<bool> PushOption = new(CliHelper.FormatAlias("push"))
        {
            Description = "Push built images to Docker registry"
        };

        private static readonly Option<bool> RetryOption = new(CliHelper.FormatAlias("retry"))
        {
            Description = "Retry building images upon failure"
        };

        private static readonly Option<bool> SkipPullingOption = new(CliHelper.FormatAlias("skip-pulling"))
        {
            Description = "Skip explicitly pulling the base images of the Dockerfiles"
        };

        private static readonly Option<string?> ImageInfoOutputPathOption = new(CliHelper.FormatAlias("image-info-output-path"))
        {
            Description = "Path to output image info"
        };

        private static readonly Option<string?> ImageInfoSourcePathOption = new(CliHelper.FormatAlias("image-info-source-path"))
        {
            Description = "Path to source image info"
        };

        private static readonly Option<string?> SourceRepoOption = new(CliHelper.FormatAlias("source-repo"))
        {
            Description = "Repo URL of the Dockerfile sources"
        };

        private static readonly Option<bool> NoCacheOption = new(CliHelper.FormatAlias("no-cache"))
        {
            Description = "Disables build cache feature"
        };

        private static readonly Option<string?> SourceRepoPrefixOption = new(CliHelper.FormatAlias("source-repo-prefix"))
        {
            Description = "Prefix to add to the external base image names when pulling them"
        };

        private static readonly Option<Dictionary<string, string>> BuildArgsOption =
            CliHelper.CreateDictionaryOption("build-arg",
                "Build argument to pass to the Dockerfiles (<name>=<value>)");

        private static readonly Option<string[]> DockerBuildOption = new(CliHelper.FormatAlias("build-option"))
        {
            Description = "Additional argument to pass directly to docker build. Repeat for multiple arguments and quote values containing spaces.",
            AllowMultipleArgumentsPerToken = true,
        };

        private static readonly Option<bool> SkipPlatformCheckOption = new(CliHelper.FormatAlias("skip-platform-check"))
        {
            Description = "Skips validation that ensures the Dockerfile's base image's platform matches the manifest configuration"
        };

        private static readonly Option<string?> DigestsOutVarOption = new(CliHelper.FormatAlias("digests-out-var"))
        {
            Description = "Azure DevOps variable name to use for outputting the list of built image digests"
        };

        private static readonly Option<bool> InternalOption = new(CliHelper.FormatAlias("internal"))
        {
            Description = "When true, all Dockerfiles will be passed the build arg ACCESSTOKEN containing the access token "
                + "for the storage account specified by the storage-service-connection option. If used without the "
                + "option, then it will use the system's default Azure credential instead."
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..FilterOptions.GetCliOptions(),
            ..BaseImageOverrideOptions.GetCliOptions(),
            ..CredentialsOptions.GetCliOptions(),
            StorageServiceConnectionOption,
            PushOption,
            RetryOption,
            SkipPullingOption,
            ImageInfoOutputPathOption,
            ImageInfoSourcePathOption,
            SourceRepoOption,
            NoCacheOption,
            SourceRepoPrefixOption,
            BuildArgsOption,
            DockerBuildOption,
            SkipPlatformCheckOption,
            DigestsOutVarOption,
            InternalOption,
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..FilterOptions.GetCliArguments(),
            ..BaseImageOverrideOptions.GetCliArguments(),
            ..CredentialsOptions.GetCliArguments(),
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            FilterOptions.Bind(result);
            BaseImageOverrideOptions.Bind(result);
            CredentialsOptions.Bind(result);
            StorageServiceConnection = result.GetValue(StorageServiceConnectionOption);
            IsPushEnabled = result.GetValue(PushOption);
            IsRetryEnabled = result.GetValue(RetryOption);
            IsSkipPullingEnabled = result.GetValue(SkipPullingOption);
            ImageInfoOutputPath = result.GetValue(ImageInfoOutputPathOption);
            ImageInfoSourcePath = result.GetValue(ImageInfoSourcePathOption);
            SourceRepoUrl = result.GetValue(SourceRepoOption);
            NoCache = result.GetValue(NoCacheOption);
            SourceRepoPrefix = result.GetValue(SourceRepoPrefixOption);
            BuildArgs = result.GetValue(BuildArgsOption) ?? new Dictionary<string, string>();
            DockerBuildOptions = result.GetValue(DockerBuildOption) ?? [];
            SkipPlatformCheck = result.GetValue(SkipPlatformCheckOption);
            OutputVariableName = result.GetValue(DigestsOutVarOption);
            Internal = result.GetValue(InternalOption);
        }
    }
}
