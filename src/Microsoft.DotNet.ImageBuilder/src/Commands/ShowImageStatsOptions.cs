// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ShowImageStatsOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public ShowImageStatsOptions() : base()
        {
        }
    }

    public class ShowImageStatsSymbolsBuilder : ManifestSymbolsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
           base.GetCliOptions().Concat(ManifestFilterOptions.GetCliOptions());
    }
}
#nullable disable
