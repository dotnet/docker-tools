// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using WebApi = Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    [Export(typeof(IVssConnectionFactory))]
    internal class VssConnectionFactory : IVssConnectionFactory
    {
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public VssConnectionFactory(ILoggerService loggerService)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public IVssConnection Create(Uri baseUrl, VssCredentials credentials)
        {
            return new VssConnectionWrapper(_loggerService, new VssConnection(baseUrl, credentials));
        }

        private class VssConnectionWrapper : IVssConnection
        {
            private readonly ILoggerService _loggerService;
            private readonly VssConnection _inner;

            public VssConnectionWrapper(ILoggerService loggerService, VssConnection inner)
            {
                _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public void Dispose()
            {
                _inner.Dispose();
            }

            public IProjectHttpClient GetProjectHttpClient()
            {
                return new ProjectHttpClientWrapper(_loggerService, _inner.GetClient<ProjectHttpClient>());
            }

            public IBuildHttpClient GetBuildHttpClient()
            {
                return new BuildHttpClientWrapper(_loggerService, _inner.GetClient<WebApi.BuildHttpClient>());
            }

            private class ProjectHttpClientWrapper : IProjectHttpClient
            {
                private readonly ILoggerService _loggerService;
                private readonly ProjectHttpClient _inner;

                public ProjectHttpClientWrapper(ILoggerService loggerService, ProjectHttpClient inner)
                {
                    _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
                    _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                }

                public void Dispose()
                {
                    _inner.Dispose();
                }

                public Task<TeamProject> GetProjectAsync(string projectId) =>
                    RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                        .ExecuteAsync(() => _inner.GetProject(projectId));
            }

            private class BuildHttpClientWrapper : IBuildHttpClient
            {
                private readonly ILoggerService _loggerService;
                private readonly WebApi.BuildHttpClient _inner;

                public BuildHttpClientWrapper(ILoggerService loggerService, WebApi.BuildHttpClient inner)
                {
                    _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
                    _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                }

                public void Dispose()
                {
                    _inner.Dispose();
                }

                public Task<List<string>> AddBuildTagAsync(Guid project, int buildId, string tag) =>
                    RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                        .ExecuteAsync(() => _inner.AddBuildTagAsync(project, buildId, tag));

                public Task<IPagedList<WebApi.Build>> GetBuildsAsync(Guid projectId, IEnumerable<int> definitions = null, WebApi.BuildStatus? statusFilter = null) =>
                    RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                        .ExecuteAsync(() => _inner.GetBuildsAsync2(projectId, definitions: definitions, statusFilter: statusFilter));

                public Task<WebApi.Build> QueueBuildAsync(WebApi.Build build) =>
                    _inner.QueueBuildAsync(build);
            }
        }
    }
}
