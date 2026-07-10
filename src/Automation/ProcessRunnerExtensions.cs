// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Convenience methods for running external processes.
/// </summary>
public static class ProcessRunnerExtensions
{
    /// <summary>
    /// Runs an external process in the current working directory.
    /// </summary>
    /// <param name="processRunner">The process runner.</param>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">The arguments passed to the executable.</param>
    /// <param name="cancellationToken">A token that cancels the process.</param>
    /// <returns>The process exit code and captured output.</returns>
    public static Task<ProcessResult> RunAsync(
        this IProcessRunner processRunner,
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        return processRunner.RunAsync(
            workingDirectory: null,
            fileName,
            arguments,
            cancellationToken);
    }
}
