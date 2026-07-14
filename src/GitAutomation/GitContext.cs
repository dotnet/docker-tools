// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.GitAutomation;

internal sealed class GitContext(string workspaceDirectory, Git git, ILogger logger) : IGitContext
{
    public string WorkspaceDirectory { get; } = workspaceDirectory;

    public async Task CommitAsync(string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string status = await git.RunAsync(secret: null, WorkspaceDirectory, cancellationToken, "status", "--porcelain");
        if (string.IsNullOrWhiteSpace(status))
        {
            logger.LogInformation("No changes to commit; working tree is clean.");
            return;
        }

        await git.RunAsync(secret: null, WorkspaceDirectory, cancellationToken, "add", "--all");
        await git.RunAsync(secret: null, WorkspaceDirectory, cancellationToken, "commit", "--message", message);

        string commit = await git.RunAsync(secret: null, WorkspaceDirectory, cancellationToken, "rev-parse", "HEAD");
        logger.LogInformation("Committed changes as {Commit}: \"{Message}\".", commit, message);
    }
}
