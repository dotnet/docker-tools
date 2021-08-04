// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.WebApi;
using WebApi = Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    public interface IBuildHttpClient : IDisposable
    {
        Task<WebApi.Build> GetBuildAsync(Guid projectId, int buildId);

        Task<IPagedList<WebApi.Build>> GetBuildsAsync(Guid projectId, IEnumerable<int> definitions = null, WebApi.BuildStatus? statusFilter = null);

        Task<WebApi.Timeline> GetBuildTimelineAsync(Guid projectId, int buildId);

        Task<WebApi.Build> QueueBuildAsync(WebApi.Build build);
    }
}
