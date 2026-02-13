#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using WebApi = Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    internal class VssConnectionFactory : IVssConnectionFactory
    {
        private readonly ILogger<VssConnectionFactory> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public VssConnectionFactory(ILogger<VssConnectionFactory> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IVssConnection Create(Uri baseUrl, VssCredentials credentials)
        {
            return new VssConnectionWrapper(_loggerFactory, new VssConnection(baseUrl, credentials));
        }

        private class VssConnectionWrapper : IVssConnection
        {
            private readonly ILogger<VssConnectionWrapper> _logger;
            private readonly ILoggerFactory _loggerFactory;
            private readonly VssConnection _inner;

            public VssConnectionWrapper(ILoggerFactory loggerFactory, VssConnection inner)
            {
                _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
                _logger = _loggerFactory.CreateLogger<VssConnectionWrapper>();
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public void Dispose()
            {
                _inner.Dispose();
            }

            public IProjectHttpClient GetProjectHttpClient()
            {
                return new ProjectHttpClientWrapper(_loggerFactory, _inner.GetClient<ProjectHttpClient>());
            }

            public IBuildHttpClient GetBuildHttpClient()
            {
                return new BuildHttpClientWrapper(_loggerFactory, _inner.GetClient<WebApi.BuildHttpClient>());
            }

            private class ProjectHttpClientWrapper : IProjectHttpClient
            {
                private readonly ILogger<ProjectHttpClientWrapper> _logger;
                private readonly ProjectHttpClient _inner;

                public ProjectHttpClientWrapper(ILoggerFactory loggerFactory, ProjectHttpClient inner)
                {
                    ArgumentNullException.ThrowIfNull(loggerFactory);
                    _logger = loggerFactory.CreateLogger<ProjectHttpClientWrapper>();
                    _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                }

                public void Dispose()
                {
                    _inner.Dispose();
                }

                public Task<TeamProject> GetProjectAsync(string projectId) =>
                    RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                        .ExecuteAsync(() => _inner.GetProject(projectId));
            }

            private class BuildHttpClientWrapper : IBuildHttpClient
            {
                private readonly ILogger<BuildHttpClientWrapper> _logger;
                private readonly WebApi.BuildHttpClient _inner;

                public BuildHttpClientWrapper(ILoggerFactory loggerFactory, WebApi.BuildHttpClient inner)
                {
                    ArgumentNullException.ThrowIfNull(loggerFactory);
                    _logger = loggerFactory.CreateLogger<BuildHttpClientWrapper>();
                    _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                }

                public void Dispose()
                {
                    _inner.Dispose();
                }

                public Task<List<string>> AddBuildTagAsync(Guid project, int buildId, string tag) =>
                    RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                        .ExecuteAsync(() => _inner.AddBuildTagAsync(project, buildId, tag));

                public Task<WebApi.Build> GetBuildAsync(Guid projectId, int buildId) =>
                    _inner.GetBuildAsync(projectId, buildId);

                public Task<IPagedList<WebApi.Build>> GetBuildsAsync(Guid projectId, IEnumerable<int> definitions = null, WebApi.BuildStatus? statusFilter = null) =>
                    RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                        .ExecuteAsync(() => _inner.GetBuildsAsync2(projectId, definitions: definitions, statusFilter: statusFilter));

                public Task<WebApi.Timeline> GetBuildTimelineAsync(Guid projectId, int buildId) =>
                    _inner.GetBuildTimelineAsync(projectId, buildId);

                public Task<WebApi.Build> QueueBuildAsync(WebApi.Build build) =>
                    _inner.QueueBuildAsync(build);
            }
        }
    }
}
