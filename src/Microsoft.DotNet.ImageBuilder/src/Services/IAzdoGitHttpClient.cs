// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Services
{
    public interface IAzdoGitHttpClient : IDisposable
    {
        Task<List<GitRepository>> GetRepositoriesAsync();
        Task<List<GitRef>> GetBranchRefsAsync(Guid repositoryId);
        Task<GitItem> GetItemAsync(Guid repositoryId, string path, GitVersionDescriptor? versionDescriptor = null);
        Task<GitPush> CreatePushAsync(GitPush push, Guid repositoryId);
        Task<GitCommit> GetCommitAsync(string commitId, Guid repositoryId);
    }
}
#nullable disable
