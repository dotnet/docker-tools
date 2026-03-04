// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    public class AzdoGitHttpClientFactory(ILoggerFactory loggerFactory) : IAzdoGitHttpClientFactory
    {
        private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        public IAzdoGitHttpClient GetClient(Uri baseUrl, VssCredentials credentials) =>
            new GitHttpClientWrapper(_loggerFactory.CreateLogger<GitHttpClientWrapper>(), new GitHttpClient(baseUrl, credentials));

        private class GitHttpClientWrapper : IAzdoGitHttpClient
        {
            private readonly ILogger<GitHttpClientWrapper> _logger;
            private readonly GitHttpClient _gitHttpClient;

            public GitHttpClientWrapper(ILogger<GitHttpClientWrapper> logger, GitHttpClient gitHttpClient)
            {
                _logger = logger;
                _gitHttpClient = gitHttpClient;
            }

            public Task<List<GitRepository>> GetRepositoriesAsync() =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                    .ExecuteAsync(() => _gitHttpClient.GetRepositoriesAsync());

            public Task<List<GitRef>> GetBranchRefsAsync(Guid repositoryId) =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                    .ExecuteAsync(() => _gitHttpClient.GetBranchRefsAsync(repositoryId));

            public Task<GitItem> GetItemAsync(Guid repositoryId, string path, GitVersionDescriptor? versionDescriptor = null) =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                    .ExecuteAsync(() => _gitHttpClient.GetItemAsync(repositoryId, path, versionDescriptor: versionDescriptor));

            public Task<GitPush> CreatePushAsync(GitPush push, Guid repositoryId) =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                    .ExecuteAsync(() => _gitHttpClient.CreatePushAsync(push, repositoryId));

            public Task<GitCommit> GetCommitAsync(string commitId, Guid repositoryId) =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                    .ExecuteAsync(() => _gitHttpClient.GetCommitAsync(commitId, repositoryId));

            public void Dispose() => _gitHttpClient.Dispose();
        }
    }
}
