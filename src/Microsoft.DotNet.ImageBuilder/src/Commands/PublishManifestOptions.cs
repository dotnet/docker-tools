// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new RegistryCredentialsOptions();
        public ServiceConnectionOptions? AcrServiceConnection { get; set; } = null;

        public string ImageInfoPath { get; set; } = string.Empty;

        public PublishManifestOptions() : base()
        {
        }
    }

    public class PublishManifestOptionsBuilder : ManifestOptionsBuilder
    {
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();
        private readonly ServiceConnectionOptionsBuilder _serviceConnectionOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            .._manifestFilterOptionsBuilder.GetCliOptions(),
            .._registryCredentialsOptionsBuilder.GetCliOptions(),
            .._serviceConnectionOptionsBuilder.GetCliOptions(
                "acr-service-connection", nameof(PublishManifestOptions.AcrServiceConnection)),
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            .._manifestFilterOptionsBuilder.GetCliArguments(),
            .._registryCredentialsOptionsBuilder.GetCliArguments(),
            new Argument<string>(nameof(PublishImageInfoOptions.ImageInfoPath),
                "Image info file path"),
        ];
    }
}
