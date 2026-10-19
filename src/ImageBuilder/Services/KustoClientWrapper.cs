// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Core;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Ingest;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;


namespace Microsoft.DotNet.ImageBuilder.Services;

internal class KustoClientWrapper(ILogger<KustoClientWrapper> logger, IAzureTokenCredentialProvider tokenCredentialProvider) : IKustoClient
{
    private readonly ILogger<KustoClientWrapper> _logger = logger;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;

    public async Task IngestFromCsvAsync(
        string csv,
        string cluster,
        string database,
        string table,
        IServiceConnection serviceConnection,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("INGESTING DATA INTO KUSTO");

        string connectionString = $"https://{cluster}.kusto.windows.net";
        TokenCredential tokenCredential = _tokenCredentialProvider.GetCredential(serviceConnection);
        KustoConnectionStringBuilder connectionBuilder = new KustoConnectionStringBuilder(connectionString)
            .WithAadAzureTokenCredentialsAuthentication(tokenCredential);

        using IKustoIngestClient client = KustoIngestFactory.CreateDirectIngestClient(connectionBuilder);
        KustoIngestionProperties properties =
            new(database, table) { Format = DataSourceFormat.csv };
        StreamSourceOptions sourceOptions = new() { SourceId = Guid.NewGuid() };

        AsyncRetryPolicy retryPolicy = Policy
            .Handle<Kusto.Data.Exceptions.KustoException>()
            .Or<Kusto.Ingest.Exceptions.KustoException>()
            .WaitAndRetryAsync(
                Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(10), RetryHelper.MaxRetries),
                RetryHelper.GetOnRetryDelegate(RetryHelper.MaxRetries, _logger));

        IKustoIngestionResult result = await retryPolicy.ExecuteAsync(
            ct => IngestFromStreamAsync(csv, client, properties, sourceOptions, ct),
            cancellationToken);

        IngestionStatus ingestionStatus = result.GetIngestionStatusBySourceId(sourceOptions.SourceId);
        for (int i = 0; i < 10 && ingestionStatus.Status == Status.Pending; i++)
        {
            _logger.LogInformation(
                $"Waiting for ingestion from source ID {sourceOptions.SourceId} to complete...");
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
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

    private async Task<IKustoIngestionResult> IngestFromStreamAsync(
        string csv,
        IKustoIngestClient client,
        KustoIngestionProperties properties,
        StreamSourceOptions sourceOptions,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            $"Ingesting {csv.Length} bytes of data to Kusto (source ID: {sourceOptions.SourceId})");

        using MemoryStream stream = new();
        using StreamWriter writer = new(stream);
        writer.Write(csv);
        writer.Flush();
        stream.Seek(0, SeekOrigin.Begin);

        return await client.IngestFromStreamAsync(stream, properties, sourceOptions);
    }
}
