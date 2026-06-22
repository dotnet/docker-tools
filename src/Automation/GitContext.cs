// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Default <see cref="IGitContext"/> implementation backed by a temporary git
/// workspace.
/// </summary>
internal sealed class GitContext(GitWorkspace workspace, GitAuthor author, ILogger logger) : IGitContext
{
    private readonly List<GitCommit> _commits = [];

    public string Directory => workspace.Path;

    public IReadOnlyList<GitCommit> Commits => _commits;

    public async Task<GitCommit?> CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (!await workspace.HasChangesAsync())
        {
            logger.LogInformation("Skipped commit '{Message}' because there were no changes.", message);
            return null;
        }

        await workspace.LogChangesAsync();
        string commitSha = await workspace.CommitAllAsync(message);
        GitCommit commit = new(commitSha, author.Name, author.Email, message);
        _commits.Add(commit);
        return commit;
    }

    public async Task ThrowIfPendingChangesAsync()
    {
        if (await workspace.HasChangesAsync())
        {
            throw new InvalidOperationException(
                "The automation callback left uncommitted changes. Call IGitContext.CommitAsync after editing files.");
        }
    }
}
