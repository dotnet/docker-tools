// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class IngestKustoImageInfoOptions : ImageInfoOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

        public string Cluster { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public ServicePrincipalOptions ServicePrincipal { get; set; } = new ServicePrincipalOptions();
        public string Table { get; set; } = string.Empty;
    }

    public class IngestKustoImageInfoOptionsBuilder : ImageInfoOptionsBuilder
    {
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder =
            new ManifestFilterOptionsBuilder();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_manifestFilterOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_manifestFilterOptionsBuilder.GetCliArguments())
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(IngestKustoImageInfoOptions.Cluster), "The cluster to ingest the data to"),
                        new Argument<string>(nameof(IngestKustoImageInfoOptions.Database), "The database to ingest the data to"),
                        new Argument<string>(nameof(IngestKustoImageInfoOptions.Table), "The table to ingest the data to"),
                    }
                )
                .Concat(ServicePrincipalOptions.GetCliArguments());
    }
}
#nullable disable
