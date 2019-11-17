// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    public interface IBuildHttpClient : IDisposable
    {
        Task<IPagedList<Build>> GetBuildsAsync(Guid projectId, IEnumerable<int> definitions = null, BuildStatus? statusFilter = null);

        Task QueueBuildAsync(Build build);
    }
}
