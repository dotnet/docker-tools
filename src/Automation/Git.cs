// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Automation;

internal static class Git
{
    public static async Task<string> RunAsync(
        ILogger logger,
        string? secret,
        string? workingDirectory,
        CancellationToken cancellationToken,
        params string[] args)
    {
        ProcessStartInfo startInfo = new("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        logger.LogDebug("Running: git {Args}", Redact(string.Join(' ', args), secret));

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                // Best effort; the process may have already exited.
                logger.LogWarning(ex, "Failed to kill git process after cancellation.");
            }
            throw;
        }

        string output = await outputTask;
        string error = await errorTask;

        // git writes progress and other human-readable messages to stderr even on success.
        if (!string.IsNullOrWhiteSpace(error))
        {
            logger.LogDebug(
                """
                git stderr:
                {Error}
                """,
                Redact(error.Trim(), secret)
            );
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            logger.LogDebug(
                """
                git stdout:
                {Output}
                """,
                Redact(output.Trim(), secret)
            );
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed with exit code {process.ExitCode}.{Environment.NewLine}{error}");
        }

        return output.Trim();
    }

    /// <summary>
    /// Scrubs a known <paramref name="secret"/> (e.g. an access token embedded in a clone URL)
    /// from text before it is logged, so tokens never reach the logs.
    /// </summary>
    private static string Redact(string text, string? secret) =>
        string.IsNullOrEmpty(secret) ? text : text.Replace(secret, "***");
}
