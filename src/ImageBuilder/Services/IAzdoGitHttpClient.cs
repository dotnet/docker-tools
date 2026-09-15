// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    public interface IAzdoGitHttpClient : IDisposable
    {
        Task<List<GitRepository>> GetRepositoriesAsync(CancellationToken cancellationToken);
        Task<List<GitRef>> GetBranchRefsAsync(Guid repositoryId, CancellationToken cancellationToken);
        Task<GitItem> GetItemAsync(Guid repositoryId, string path, CancellationToken cancellationToken, GitVersionDescriptor? versionDescriptor = null);
        Task<GitPush> CreatePushAsync(GitPush push, Guid repositoryId, CancellationToken cancellationToken);
        Task<GitCommit> GetCommitAsync(string commitId, Guid repositoryId, CancellationToken cancellationToken);
    }
}
