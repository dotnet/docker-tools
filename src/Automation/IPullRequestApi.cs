// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// The service-specific operations needed to manage pull requests and their
/// comments. All git operations are service-agnostic and handled by
/// <see cref="RepoHostEngine"/>; implementations of this interface only deal
/// with the pull request itself. A host service (GitHub, Azure DevOps) provides
/// an implementation; tests can provide an in-memory fake.
/// </summary>
public interface IPullRequestApi
{
    /// <summary>
    /// Finds the open pull request from <paramref name="headBranch"/> into
    /// <paramref name="targetBranch"/>, or null if there is none.
    /// </summary>
    Task<PullRequestInfo?> FindOpenAsync(string headBranch, string targetBranch, CancellationToken cancellationToken);

    Task<PullRequestInfo> CreateAsync(
        string title, string body, string headBranch, string targetBranch, CancellationToken cancellationToken);

    Task UpdateAsync(long id, string title, string body, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetCommentsAsync(long id, CancellationToken cancellationToken);

    Task AddCommentAsync(long id, string comment, CancellationToken cancellationToken);
}
