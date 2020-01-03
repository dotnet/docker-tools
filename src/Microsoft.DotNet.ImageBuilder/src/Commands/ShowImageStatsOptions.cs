// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ShowImageStatsOptions : ManifestOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Displays statistics about the number of images";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public ShowImageStatsOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);
        }
    }
}
