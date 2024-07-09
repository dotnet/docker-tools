// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

public interface IAzureLogService
{
    Task<List<AcrEventEntry>> GetRecentPushEntries(string repository, string tag, string acrLogsWorkspaceId, int logsQueryDayRange);
}

