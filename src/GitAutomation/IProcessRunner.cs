// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation;

/// <summary>
/// Runs external processes and captures their output.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs an external process.
    /// </summary>
    /// <param name="workingDirectory">
    /// The process working directory, or <see langword="null"/> to use the current directory.
    /// </param>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">The arguments passed to the executable.</param>
    /// <param name="cancellationToken">A token that cancels the process.</param>
    /// <returns>The process exit code and captured output.</returns>
    Task<ProcessResult> RunAsync(
        string? workingDirectory,
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken);
}

/// <summary>
/// The result of running an external process.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">The captured standard output.</param>
/// <param name="StandardError">The captured standard error.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
