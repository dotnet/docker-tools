// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Commands;
using Octokit;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

public interface IOctokitClientFactory
{
    Task<IGitHubClient> CreateGitHubClientAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken);

    Task<IBlobsClient> CreateBlobsClientAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken);

    Task<ITreesClient> CreateTreesClientAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken);

    Task<string> CreateGitHubTokenAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken);
}
