// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateVersionsOptions : ManifestOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Updates the version information for the dependent images";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();
        public GitOptions GitOptions { get; } = new GitOptions("dotnet", "versions", "master", "build-info/docker");

        public UpdateVersionsOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            FilterOptions.ParseCommandLine(syntax);
            GitOptions.ParseCommandLine(syntax);
        }
    }
}
