// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Automation;

internal sealed class GitWorkspace(string directory, ILogger logger) : IDisposable
{
    public string WorkingDirectory { get; } = directory;

    public static async Task<GitWorkspace> CloneAsync(
        Git git,
        ILogger logger,
        Uri cloneUrl,
        string branch,
        string authorName,
        string authorEmail,
        CancellationToken ct)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"git-workspace-{Path.GetRandomFileName()}");

        // The clone URL embeds the access token as "x-access-token:TOKEN"; scrub that from logs.
        string secret = cloneUrl.UserInfo;

        var runGit = async (string[] args, string? directory = null) =>
            await git.RunAsync(secret, directory, ct, args);

        try
        {
            await runGit([
                "clone",
                "--filter=blob:none",
                "--single-branch",
                "--no-tags",
                "--branch",
                branch,
                cloneUrl.AbsoluteUri,
                directory,
            ]);

            await runGit(["config", "user.name", authorName], directory);
            await runGit(["config", "user.email", authorEmail], directory);
        }
        catch (Exception exception) when (Directory.Exists(directory))
        {
            logger.LogWarning(exception, "Clone into {Directory} failed; cleaning up.", directory);
            DeleteDirectory(logger, directory);
            throw;
        }

        return new GitWorkspace(directory, logger);
    }

    public void Dispose()
    {
        DeleteDirectory(logger, WorkingDirectory);
    }

    private static void DeleteDirectory(ILogger logger, string workingDirectory)
    {
        if (!Directory.Exists(workingDirectory))
        {
            return;
        }

        logger.LogInformation("Cleaning up temporary workspace {Directory}.", workingDirectory);

        try
        {
            // git marks objects under .git as read-only, which blocks Directory.Delete on Windows.
            // Clear the read-only attribute on every file first.
            foreach (string file in Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(workingDirectory, recursive: true);
        }
        catch (Exception exception)
        {
            // Best effort; ignore any failures cleaning up the temporary workspace.
            logger.LogWarning(exception, "Failed to delete temporary workspace {Directory}.", workingDirectory);
        }
    }
}
