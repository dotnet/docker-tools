// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class PublishManifestOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new RegistryCredentialsOptions();

        public string ImageInfoPath { get; set; } = string.Empty;

        public PublishManifestOptions() : base()
        {
        }
    }

    public class PublishManifestOptionsBuilder : ManifestOptionsBuilder
    {
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder =
            new ManifestFilterOptionsBuilder();
        private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder =
            new RegistryCredentialsOptionsBuilder();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_manifestFilterOptionsBuilder.GetCliOptions())
                .Concat(_registryCredentialsOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_manifestFilterOptionsBuilder.GetCliArguments())
                .Concat(_registryCredentialsOptionsBuilder.GetCliArguments())
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(PublishImageInfoOptions.ImageInfoPath),
                            "Image info file path")
                    });
    }
}
#nullable disable
