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

        public VssConnectionFactory(ILogger<VssConnectionFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IVssConnection Create(Uri baseUrl, VssCredentials credentials)
        {
            return new VssConnectionWrapper(StandaloneLoggerFactory.CreateLogger<VssConnectionWrapper>(), new VssConnection(baseUrl, credentials));
        }

        private class VssConnectionWrapper : IVssConnection
        {
            private readonly ILogger<VssConnectionWrapper> _logger;
            private readonly VssConnection _inner;

            public VssConnectionWrapper(ILogger<VssConnectionWrapper> logger, VssConnection inner)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public void Dispose()
            {
                _inner.Dispose();
            }

            public IProjectHttpClient GetProjectHttpClient()
            {
                return new ProjectHttpClientWrapper(StandaloneLoggerFactory.CreateLogger<ProjectHttpClientWrapper>(), _inner.GetClient<ProjectHttpClient>());
            }

            public IBuildHttpClient GetBuildHttpClient()
            {
                return new BuildHttpClientWrapper(StandaloneLoggerFactory.CreateLogger<BuildHttpClientWrapper>(), _inner.GetClient<WebApi.BuildHttpClient>());
            }

            private class ProjectHttpClientWrapper : IProjectHttpClient
            {
                private readonly ILogger<ProjectHttpClientWrapper> _logger;
                private readonly ProjectHttpClient _inner;

                public ProjectHttpClientWrapper(ILogger<ProjectHttpClientWrapper> logger, ProjectHttpClient inner)
                {
                    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

                public BuildHttpClientWrapper(ILogger<BuildHttpClientWrapper> logger, WebApi.BuildHttpClient inner)
                {
                    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
