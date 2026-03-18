// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class IngestKustoImageInfoOptions : ImageInfoOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new();
        public ServiceConnection? KustoServiceConnection { get; set; }
        public string Cluster { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string ImageTable { get; set; } = string.Empty;
        public string LayerTable { get; set; } = string.Empty;

        private static readonly ServiceConnectionOptionsBuilder ServiceConnectionBuilder = new();

        private static readonly Option<ServiceConnection?> KustoServiceConnectionOption =
            ServiceConnectionBuilder.GetCliOption("kusto-service-connection");

        private static readonly Argument<string> ClusterArgument = new(nameof(Cluster))
        {
            Description = "The cluster to ingest the data to"
        };

        private static readonly Argument<string> DatabaseArgument = new(nameof(Database))
        {
            Description = "The database to ingest the data to"
        };

        private static readonly Argument<string> ImageTableArgument = new(nameof(ImageTable))
        {
            Description = "The image table to ingest the data to"
        };

        private static readonly Argument<string> LayerTableArgument = new(nameof(LayerTable))
        {
            Description = "The layer table to ingest the data to"
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..FilterOptions.GetCliOptions(),
            KustoServiceConnectionOption,
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..FilterOptions.GetCliArguments(),
            ClusterArgument,
            DatabaseArgument,
            ImageTableArgument,
            LayerTableArgument,
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            FilterOptions.Bind(result);
            KustoServiceConnection = result.GetValue(KustoServiceConnectionOption);
            Cluster = result.GetValue(ClusterArgument) ?? string.Empty;
            Database = result.GetValue(DatabaseArgument) ?? string.Empty;
            ImageTable = result.GetValue(ImageTableArgument) ?? string.Empty;
            LayerTable = result.GetValue(LayerTableArgument) ?? string.Empty;
        }
    }
}
