// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder;

public interface IGitHubClientFactory
{
    Task<IGitHubClient> GetClientAsync(GitOptions gitOptions, bool isDryRun, CancellationToken cancellationToken);
}
