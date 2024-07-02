// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure;
using Azure.Identity;
using Azure.Monitor.Query.Models;
using Azure.Monitor.Query;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IAzureLogService))]
    public class AzureLogService : IAzureLogService
    {
        private string AcrLogsWorkspaceId = "4db2f537-e3cf-4a80-97ba-9e2e66717407";
        private const int TimespanDays = 7;

        public async Task<List<AcrEventEntry>> GetRecentPushEntries(string repository, string tag)
        {
            List<AcrEventEntry> entries = [];

            LogsTable? logsTable = await GetLogDataTable(
                $"ContainerRegistryRepositoryEvents | where OperationName == 'Push' | where Repository == '{repository}' | where Tag == '{tag}' | sort by TimeGenerated asc",
                TimespanDays);

            foreach (LogsTableRow row in logsTable.Rows)
            {
                entries.Add(new AcrEventEntry
                {
                    TimeGenerated = DateTime.Parse(row["TimeGenerated"].ToString()),
                    Digest = row["Digest"].ToString()
                });
            }

            return entries.DistinctBy(x => x.Digest).ToList();
        }

        private async Task<LogsTable> GetLogDataTable(string query, int timespanDays)
        {
            var client = new LogsQueryClient(new DefaultAzureCredential());

            Response<LogsQueryResult> result = await client.QueryWorkspaceAsync(
                AcrLogsWorkspaceId,
                query,
                new QueryTimeRange(TimeSpan.FromDays(timespanDays)));

            return result.Value.Table;
        }
    }
}
