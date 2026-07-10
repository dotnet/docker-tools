// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Runs external processes using <see cref="Process"/>.
/// </summary>
/// <param name="logger">The logger used to report process cleanup failures.</param>
public sealed class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    /// <inheritdoc/>
    public async Task<ProcessResult> RunAsync(
        string? workingDirectory,
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        ProcessStartInfo startInfo = new(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{startInfo.FileName}'.");

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
            catch (Exception exception)
            {
                // The process may have already exited.
                logger.LogWarning(exception, "Failed to kill process after cancellation.");
            }

            throw;
        }

        return new ProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }
}
