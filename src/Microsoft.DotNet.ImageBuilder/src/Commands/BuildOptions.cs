// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new();
        public BaseImageOverrideOptions BaseImageOverrideOptions { get; set; } = new();
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();
        public ServiceConnectionOptions? AcrServiceConnection { get; set; }
        public ServiceConnectionOptions? StorageServiceConnection { get; set; }

        public bool IsPushEnabled { get; set; }
        public bool IsRetryEnabled { get; set; }
        public bool IsSkipPullingEnabled { get; set; }
        public string? ImageInfoOutputPath { get; set; }
        public string? ImageInfoSourcePath { get; set; }
        public string? SourceRepoUrl { get; set; }
        public bool NoCache { get; set; }
        public string? SourceRepoPrefix { get; set; }
        public IDictionary<string, string> BuildArgs { get; set; } = new Dictionary<string, string>();
        public bool SkipPlatformCheck { get; set; }
        public string? OutputVariableName { get; set; }
        public string? Subscription { get; set; }
        public string? ResourceGroup { get; set; }
        public bool Internal { get; set; }
    }

    public class BuildOptionsBuilder : ManifestOptionsBuilder
    {
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();
        private readonly BaseImageOverrideOptionsBuilder _baseImageOverrideOptionsBuilder = new();
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();
        private readonly ServiceConnectionOptionsBuilder _serviceConnectionOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            .._manifestFilterOptionsBuilder.GetCliOptions(),
            .._baseImageOverrideOptionsBuilder.GetCliOptions(),
            .._registryCredentialsOptionsBuilder.GetCliOptions(),
            .._serviceConnectionOptionsBuilder.GetCliOptions(
                alias: "acr-service-connection",
                propertyName: nameof(BuildOptions.AcrServiceConnection)),
            .._serviceConnectionOptionsBuilder.GetCliOptions(
                alias: "storage-service-connection",
                propertyName: nameof(BuildOptions.StorageServiceConnection),
                description: "Storage account to use for internal builds."),

            CreateOption<bool>("push", nameof(BuildOptions.IsPushEnabled),
                "Push built images to Docker registry"),
            CreateOption<bool>("retry", nameof(BuildOptions.IsRetryEnabled),
                "Retry building images upon failure"),
            CreateOption<bool>("skip-pulling", nameof(BuildOptions.IsSkipPullingEnabled),
                "Skip explicitly pulling the base images of the Dockerfiles"),
            CreateOption<string?>("image-info-output-path", nameof(BuildOptions.ImageInfoOutputPath),
                "Path to output image info"),
            CreateOption<string?>("image-info-source-path", nameof(BuildOptions.ImageInfoSourcePath),
                "Path to source image info"),
            CreateOption<string?>("source-repo", nameof(BuildOptions.SourceRepoUrl),
                "Repo URL of the Dockerfile sources"),
            CreateOption<bool>("no-cache", nameof(BuildOptions.NoCache),
                "Disables build cache feature"),
            CreateOption<string?>("source-repo-prefix", nameof(BuildOptions.SourceRepoPrefix),
                "Prefix to add to the external base image names when pulling them"),
            CreateDictionaryOption("build-arg", nameof(BuildOptions.BuildArgs),
                "Build argument to pass to the Dockerfiles (<name>=<value>)"),
            CreateOption<bool>("skip-platform-check", nameof(BuildOptions.SkipPlatformCheck),
                "Skips validation that ensures the Dockerfile's base image's platform matches the manifest configuration"),
            CreateOption<string>("digests-out-var", nameof(BuildOptions.OutputVariableName),
                "Azure DevOps variable name to use for outputting the list of built image digests"),
            CreateOption<string>("acr-subscription", nameof(BuildOptions.Subscription),
                "Azure subscription to operate on"),
            CreateOption<string>("acr-resource-group", nameof(BuildOptions.ResourceGroup),
                "Azure resource group to operate on"),
            CreateOption<bool>("internal", nameof(BuildOptions.Internal),
                "When true, all Dockerfiles will be passed the build arg ACCESSTOKEN containing the access token "
                + "for the storage account specified by the storage-service-connection option. If used without the "
                + "option, then it will use the system's default Azure credential instead."),
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_manifestFilterOptionsBuilder.GetCliArguments())
                .Concat(_baseImageOverrideOptionsBuilder.GetCliArguments())
                .Concat(_registryCredentialsOptionsBuilder.GetCliArguments());
    }
}
