// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Ingest;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    [Export(typeof(IKustoClient))]
    internal class KustoClientWrapper : IKustoClient
    {
        public async Task IngestFromCsvStreamAsync(Stream csv, IngestKustoImageInfoOptions Options)
        {
            KustoConnectionStringBuilder connectionBuilder =
                new KustoConnectionStringBuilder($"https://{Options.Cluster}.kusto.windows.net")
                    .WithAadApplicationKeyAuthentication(Options.ClientID, Options.Secret, Options.Tenant);

            using (IKustoIngestClient client = KustoIngestFactory.CreateDirectIngestClient(connectionBuilder))
            {
                KustoIngestionProperties properties =
                    new KustoIngestionProperties(Options.Database, Options.Table) { Format = DataSourceFormat.csv };
                StreamSourceOptions sourceOptions = new StreamSourceOptions() { SourceId = Guid.NewGuid() };

                if (!Options.IsDryRun)
                {
                    IKustoIngestionResult result = await client.IngestFromStreamAsync(csv, properties, sourceOptions);

                    IngestionStatus ingestionStatus = result.GetIngestionStatusBySourceId(sourceOptions.SourceId);
                    while (ingestionStatus.Status == Status.Pending)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                        ingestionStatus = result.GetIngestionStatusBySourceId(sourceOptions.SourceId);
                    }

                    if (ingestionStatus.Status != Status.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to ingest Kusto data.{Environment.NewLine}{ingestionStatus.Details}");
                    }
                }
            }
        }
    }
}
