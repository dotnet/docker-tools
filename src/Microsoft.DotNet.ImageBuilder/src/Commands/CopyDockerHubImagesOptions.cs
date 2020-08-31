// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyDockerHubImagesOptions : CopyImagesOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Copies external base images from Docker Hub to ACR";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);
        }
    }
}
