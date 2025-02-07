// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Services
{
    [Export(typeof(IAzdoGitHttpClientFactory))]
    public class AzdoGitHttpClientFactory : IAzdoGitHttpClientFactory
    {
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public AzdoGitHttpClientFactory(ILoggerService loggerService)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public IAzdoGitHttpClient GetClient(Uri baseUrl, VssCredentials credentials) =>
            new GitHttpClientWrapper(_loggerService, new GitHttpClient(baseUrl, credentials));

        private class GitHttpClientWrapper : IAzdoGitHttpClient
        {
            private readonly ILoggerService _loggerService;
            private readonly GitHttpClient _gitHttpClient;

            public GitHttpClientWrapper(ILoggerService loggerService, GitHttpClient gitHttpClient)
            {
                _loggerService = loggerService;
                _gitHttpClient = gitHttpClient;
            }

            public Task<List<GitRepository>> GetRepositoriesAsync() =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                    .ExecuteAsync(() => _gitHttpClient.GetRepositoriesAsync());

            public Task<List<GitRef>> GetBranchRefsAsync(Guid repositoryId) =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                    .ExecuteAsync(() => _gitHttpClient.GetBranchRefsAsync(repositoryId));

            public Task<GitItem> GetItemAsync(Guid repositoryId, string path, GitVersionDescriptor? versionDescriptor = null) =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                    .ExecuteAsync(() => _gitHttpClient.GetItemAsync(repositoryId, path, versionDescriptor: versionDescriptor));

            public Task<GitPush> CreatePushAsync(GitPush push, Guid repositoryId) =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                    .ExecuteAsync(() => _gitHttpClient.CreatePushAsync(push, repositoryId));

            public Task<GitCommit> GetCommitAsync(string commitId, Guid repositoryId) =>
                RetryHelper.GetWaitAndRetryPolicy<Exception>(_loggerService)
                    .ExecuteAsync(() => _gitHttpClient.GetCommitAsync(commitId, repositoryId));

            public void Dispose() => _gitHttpClient.Dispose();
        }
    }
}
#nullable disable
