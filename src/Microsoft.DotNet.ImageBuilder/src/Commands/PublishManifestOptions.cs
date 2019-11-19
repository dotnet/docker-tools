// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestOptions : DockerRegistryOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Creates and publishes the manifest to the Docker Registry";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public PublishManifestOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            FilterOptions.ParseCommandLine(syntax);
        }
    }
}
