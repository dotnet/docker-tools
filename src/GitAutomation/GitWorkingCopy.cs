// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.GitAutomation;

/// <summary>Owns a temporary local Git working copy.</summary>
/// <remarks>Creates a temporary Git working copy.</remarks>
/// <param name="processRunner">Runs Git processes.</param>
/// <param name="logger">Records Git operations and cleanup failures.</param>
public sealed class GitWorkingCopy(IProcessRunner processRunner, ILogger logger) : IGitContext, IDisposable
{
    /// <inheritdoc/>
    public string WorkspaceDirectory { get; } = Directory.CreateTempSubdirectory("git-working-copy-").FullName;

    /// <summary>Clones one branch into this working copy.</summary>
    /// <param name="remoteUrl">The remote repository URL.</param>
    /// <param name="getAuthorizationHeader">Gets the current HTTP authorization header value.</param>
    /// <param name="branch">The branch to clone.</param>
    /// <param name="identity">The identity used for commits.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    public async Task CloneAsync(
        Uri remoteUrl,
        Func<CancellationToken, ValueTask<string>> getAuthorizationHeader,
        string branch,
        AutomationIdentity identity,
        CancellationToken cancellationToken)
    {
        await RunGitWithRemoteAsync(remoteUrl, getAuthorizationHeader, null, cancellationToken, [
            "clone",
            "--single-branch",
            "--no-tags",
            "--branch",
            branch,
            remoteUrl.AbsoluteUri,
            WorkspaceDirectory,
        ]);

        await RunGitAsync(cancellationToken, "config", "user.name", identity.AuthorName);
        await RunGitAsync(cancellationToken, "config", "user.email", identity.AuthorEmail);
    }

    /// <inheritdoc/>
    public async Task CommitAsync(string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string status = await RunGitAsync(cancellationToken, "status", "--porcelain");
        if (string.IsNullOrWhiteSpace(status))
        {
            logger.LogInformation("No changes to commit; working tree is clean.");
            return;
        }

        await RunGitAsync(cancellationToken, "add", "--all");
        await RunGitAsync(cancellationToken, "commit", "--message", message);

        string commit = await GetHeadCommitAsync(cancellationToken);
        logger.LogInformation("Committed changes as {Commit}: \"{Message}\".", commit, message);
    }

    /// <summary>Pushes this working copy's HEAD to a remote branch.</summary>
    /// <param name="remoteUrl">The remote repository URL.</param>
    /// <param name="getAuthorizationHeader">Gets the current HTTP authorization header value.</param>
    /// <param name="branch">The destination branch.</param>
    /// <param name="force">Whether to force-push the branch.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    public Task PushAsync(
        Uri remoteUrl,
        Func<CancellationToken, ValueTask<string>> getAuthorizationHeader,
        string branch,
        bool force,
        CancellationToken cancellationToken)
    {
        string remoteRef = $"refs/heads/{branch}";
        string[] arguments = force
            ? ["push", "--force", remoteUrl.AbsoluteUri, $"HEAD:{remoteRef}"]
            : ["push", remoteUrl.AbsoluteUri, $"HEAD:{remoteRef}"];

        return RunGitWithRemoteAsync(
            remoteUrl,
            getAuthorizationHeader,
            WorkspaceDirectory,
            cancellationToken,
            arguments);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DeleteDirectory(WorkspaceDirectory);
    }

    /// <summary>Gets the tree hash for HEAD.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The tree hash.</returns>
    public Task<string> GetTreeHashAsync(CancellationToken cancellationToken)
    {
        return RunGitAsync(cancellationToken, "rev-parse", "HEAD^{tree}");
    }

    /// <summary>Gets the commit hash for HEAD.</summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The commit hash.</returns>
    public Task<string> GetHeadCommitAsync(CancellationToken cancellationToken)
    {
        return RunGitAsync(cancellationToken, "rev-parse", "HEAD");
    }

    private Task<string> RunGitAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        return RunGitAsync(environment: null, WorkspaceDirectory, cancellationToken, arguments);
    }

    private async Task<string> RunGitWithRemoteAsync(
        Uri remoteUrl,
        Func<CancellationToken, ValueTask<string>> getAuthorizationHeader,
        string? workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        string authorizationHeader = await getAuthorizationHeader(cancellationToken);
        string remoteScope = remoteUrl.AbsoluteUri.TrimEnd('/');
        IReadOnlyDictionary<string, string> environment = new Dictionary<string, string>
        {
            ["GIT_CONFIG_COUNT"] = "2",
            ["GIT_CONFIG_KEY_0"] = $"http.{remoteScope}.extraheader",
            ["GIT_CONFIG_VALUE_0"] = $"AUTHORIZATION: {authorizationHeader}",
            ["GIT_CONFIG_KEY_1"] = "credential.helper",
            ["GIT_CONFIG_VALUE_1"] = string.Empty,
            ["GIT_TERMINAL_PROMPT"] = "0",
        };

        return await RunGitAsync(environment, workingDirectory, cancellationToken, arguments);
    }

    private async Task<string> RunGitAsync(
        IReadOnlyDictionary<string, string>? environment,
        string? workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        logger.LogDebug("Running: git {Args}", string.Join(' ', arguments));

        ProcessResult result = await processRunner.RunAsync(
            workingDirectory,
            "git",
            arguments,
            environment,
            cancellationToken);

        string output = result.StandardOutput;
        string error = result.StandardError;

        // Git writes progress and other informational messages to stderr on success.
        if (!string.IsNullOrWhiteSpace(error))
        {
            logger.LogDebug("git stderr:{NewLine}{Error}", Environment.NewLine, error.Trim());
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            logger.LogDebug("git stdout:{NewLine}{Output}", Environment.NewLine, output.Trim());
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed with exit code {result.ExitCode}.{Environment.NewLine}{error}");
        }

        return output.Trim();
    }

    private void DeleteDirectory(string workingDirectory)
    {
        if (!Directory.Exists(workingDirectory))
        {
            return;
        }

        logger.LogInformation("Cleaning up temporary workspace {Directory}.", workingDirectory);

        try
        {
            // Git marks objects under .git as read-only, which blocks Directory.Delete on Windows.
            // Clear the read-only attribute on every file first.
            foreach (string file in Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(workingDirectory, recursive: true);
        }
        catch (Exception exception)
        {
            // Cleanup is best effort because Dispose cannot report a failure to the caller.
            logger.LogWarning(exception, "Failed to delete temporary workspace {Directory}.", workingDirectory);
        }
    }
}
