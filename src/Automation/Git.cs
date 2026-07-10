// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Automation;

internal sealed class Git(IProcessRunner processRunner, ILogger logger)
{
    public async Task<string> RunAsync(
        string? secret,
        string? workingDirectory,
        CancellationToken cancellationToken,
        params string[] args)
    {
        logger.LogDebug("Running: git {Args}", Redact(string.Join(' ', args), secret));

        ProcessResult result = await processRunner.RunAsync(
            workingDirectory,
            fileName: "git",
            args,
            cancellationToken);
        string output = result.StandardOutput;
        string error = result.StandardError;

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

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed with exit code {result.ExitCode}.{Environment.NewLine}{error}");
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
