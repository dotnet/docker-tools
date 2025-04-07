// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Commands;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

public interface IOctokitClientFactory
{
    IGitHubClient CreateGitHubClient(GitHubAuthOptions authOptions);

    IBlobsClient CreateBlobsClient(GitHubAuthOptions authOptions);

    ITreesClient CreateTreesClient(GitHubAuthOptions authOptions);
}
