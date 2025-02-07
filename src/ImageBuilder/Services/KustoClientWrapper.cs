// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Ingest;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Services
{
    [Export(typeof(IKustoClient))]
    internal class KustoClientWrapper : IKustoClient
    {
        private readonly ILoggerService _loggerService;
        private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;

        [ImportingConstructor]
        public KustoClientWrapper(ILoggerService loggerService, IAzureTokenCredentialProvider tokenCredentialProvider)
        {
            _loggerService = loggerService;
            _tokenCredentialProvider = tokenCredentialProvider;
        }

        public async Task IngestFromCsvAsync(
            string csv, string cluster, string database, string table, bool isDryRun)
        {
            string clusterResource = $"https://{cluster}.kusto.windows.net";
            KustoConnectionStringBuilder connectionBuilder =
                new KustoConnectionStringBuilder(clusterResource)
                    .WithAadAzureTokenCredentialsAuthentication(_tokenCredentialProvider.GetCredential(clusterResource + AzureScopes.ScopeSuffix));

            using (IKustoIngestClient client = KustoIngestFactory.CreateDirectIngestClient(connectionBuilder))
            {
                KustoIngestionProperties properties =
                    new(database, table) { Format = DataSourceFormat.csv };
                StreamSourceOptions sourceOptions = new() { SourceId = Guid.NewGuid() };

                if (!isDryRun)
                {
                    AsyncRetryPolicy retryPolicy = Policy
                        .Handle<Kusto.Data.Exceptions.KustoException>()
                        .Or<Kusto.Ingest.Exceptions.KustoException>()
                        .WaitAndRetryAsync(
                            Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(10), RetryHelper.MaxRetries),
                            RetryHelper.GetOnRetryDelegate(RetryHelper.MaxRetries, _loggerService));

                    IKustoIngestionResult result = await retryPolicy.ExecuteAsync(
                        () => IngestFromStreamAsync(csv, client, properties, sourceOptions));

                    IngestionStatus ingestionStatus = result.GetIngestionStatusBySourceId(sourceOptions.SourceId);
                    for (int i = 0; i < 10 && ingestionStatus.Status == Status.Pending; i++)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        ingestionStatus = result.GetIngestionStatusBySourceId(sourceOptions.SourceId);
                    }

                    if (ingestionStatus.Status == Status.Pending)
                    {
                        throw new InvalidOperationException($"Timeout while ingesting Kusto data.");
                    }
                    else if (ingestionStatus.Status != Status.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to ingest Kusto data.{Environment.NewLine}{ingestionStatus.Details}");
                    }
                }
            }
        }

        private async Task<IKustoIngestionResult> IngestFromStreamAsync(
            string csv, IKustoIngestClient client, KustoIngestionProperties properties, StreamSourceOptions sourceOptions)
        {
            using MemoryStream stream = new();
            using StreamWriter writer = new(stream);
            writer.Write(csv);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            return await client.IngestFromStreamAsync(stream, properties, sourceOptions);
        }
    }
}
#nullable disable
