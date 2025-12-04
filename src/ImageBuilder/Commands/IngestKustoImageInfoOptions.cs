// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Configuration;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class IngestKustoImageInfoOptions : ImageInfoOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new();
        public ServiceConnectionOptions? KustoServiceConnection { get; set; }
        public string Cluster { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string ImageTable { get; set; } = string.Empty;
        public string LayerTable { get; set; } = string.Empty;
    }

    public class IngestKustoImageInfoOptionsBuilder : ImageInfoOptionsBuilder
    {
        private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();
        private readonly ServiceConnectionOptionsBuilder _serviceConnectionOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            .._manifestFilterOptionsBuilder.GetCliOptions(),
            .._serviceConnectionOptionsBuilder.GetCliOptions(
                "kusto-service-connection",
                nameof(IngestKustoImageInfoOptions.KustoServiceConnection))
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            .._manifestFilterOptionsBuilder.GetCliArguments(),
            new Argument<string>(nameof(IngestKustoImageInfoOptions.Cluster), "The cluster to ingest the data to"),
            new Argument<string>(nameof(IngestKustoImageInfoOptions.Database), "The database to ingest the data to"),
            new Argument<string>(nameof(IngestKustoImageInfoOptions.ImageTable), "The image table to ingest the data to"),
            new Argument<string>(nameof(IngestKustoImageInfoOptions.LayerTable), "The layer table to ingest the data to")
        ];
    }
}
