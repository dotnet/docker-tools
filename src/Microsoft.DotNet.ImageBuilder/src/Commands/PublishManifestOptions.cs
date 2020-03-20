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

        public string ImageInfoPath { get; set; }

        public PublishManifestOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);
        }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string imageInfoPath = null;
            syntax.DefineParameter(
                "image-info-path",
                ref imageInfoPath,
                "Image info file path");
            ImageInfoPath = imageInfoPath;
        }
    }
}
