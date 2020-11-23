// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    [Export(typeof(IVssConnectionFactory))]
    internal class VssConnectionFactory : IVssConnectionFactory
    {
        public IVssConnection Create(Uri baseUrl, VssCredentials credentials)
        {
            return new VssConnectionWrapper(new VssConnection(baseUrl, credentials));
        }

        private class VssConnectionWrapper : IVssConnection
        {
            private readonly VssConnection _inner;

            public VssConnectionWrapper(VssConnection inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public void Dispose()
            {
                _inner.Dispose();
            }

            public IProjectHttpClient GetProjectHttpClient()
            {
                return new ProjectHttpClientWrapper(_inner.GetClient<ProjectHttpClient>());
            }

            public IBuildHttpClient GetBuildHttpClient()
            {
                return new BuildHttpClientWrapper(_inner.GetClient<BuildHttpClient>());
            }

            private class ProjectHttpClientWrapper : IProjectHttpClient
            {
                private readonly ProjectHttpClient _inner;

                public ProjectHttpClientWrapper(ProjectHttpClient inner)
                {
                    _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                }

                public void Dispose()
                {
                    _inner.Dispose();
                }

                public Task<TeamProject> GetProjectAsync(string projectId)
                {
                    return _inner.GetProject(projectId);
                }
            }

            private class BuildHttpClientWrapper : IBuildHttpClient
            {
                private readonly BuildHttpClient _inner;

                public BuildHttpClientWrapper(BuildHttpClient inner)
                {
                    _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                }

                public void Dispose()
                {
                    _inner.Dispose();
                }

                public Task<IPagedList<Build>> GetBuildsAsync(Guid projectId, IEnumerable<int> definitions = null, BuildStatus? statusFilter = null)
                {
                    return _inner.GetBuildsAsync2(projectId, definitions: definitions, statusFilter: statusFilter);
                }

                public Task QueueBuildAsync(Build build)
                {
                    return _inner.QueueBuildAsync(build);
                }
            }
        }
    }
}
