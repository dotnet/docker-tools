// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

[Export(typeof(IAzureLogService))]
public class AzureLogService : IAzureLogService
{
    private const string DigestField = "Digest";
    private const string TimeGeneratedField = "TimeGenerated";

    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;

    [ImportingConstructor]
    public AzureLogService(IAzureTokenCredentialProvider tokenCredentialProvider)
    {
        _tokenCredentialProvider = tokenCredentialProvider;
    }

    public async Task<List<AcrEventEntry>> GetRecentPushEntries(string repository, string tag, string acrLogsWorkspaceId, int logsQueryDayRange)
    {
        List<AcrEventEntry> entries = [];

        LogsTable? logsTable = await GetLogDataTable(
            $"ContainerRegistryRepositoryEvents | where OperationName == 'Push' | where Repository == '{repository}' | where Tag == '{tag}' | sort by {TimeGeneratedField} asc",
            acrLogsWorkspaceId,
            logsQueryDayRange);

        foreach (LogsTableRow row in logsTable.Rows)
        {
            string? timeGenerated = (row[TimeGeneratedField]?.ToString()) ?? throw new Exception($"Missing '{TimeGeneratedField}'");
            string? digest = (row[DigestField]?.ToString()) ?? throw new Exception($"Missing '{DigestField}'");

            entries.Add(new AcrEventEntry(DateTime.Parse(timeGenerated), digest));
        }

        return entries.DistinctBy(x => x.Digest).ToList();
    }

    private async Task<LogsTable> GetLogDataTable(string query, string acrLogsWorkspaceId, int logsQueryDayRange)
    {
        var client = new LogsQueryClient(_tokenCredentialProvider.GetCredential());

        Response<LogsQueryResult> result = await client.QueryWorkspaceAsync(
            acrLogsWorkspaceId,
            query,
            new QueryTimeRange(TimeSpan.FromDays(logsQueryDayRange)));

        return result.Value.Table;
    }
}
