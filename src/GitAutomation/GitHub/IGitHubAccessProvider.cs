// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation.GitHub;

/// <summary>
/// Provides access to GitHub repositories.
/// </summary>
public interface IGitHubAccessProvider
{
    /// <summary>
    /// Gets credentials and identity that are valid for the specified repository.
    /// </summary>
    /// <param name="repository">The repository to access.</param>
    /// <param name="cancellationToken">A token that cancels access acquisition.</param>
    /// <returns>Credentials and identity for the repository.</returns>
    ValueTask<GitHubAccess> GetAccessAsync(GitHubRepo repository, CancellationToken cancellationToken);
}
