// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// Thrown when a git command fails. Credentials are masked in all properties.
/// </summary>
public sealed class GitException(string command, int exitCode, string standardError)
    : Exception($"'{command}' failed with exit code {exitCode}:{Environment.NewLine}{standardError}")
{
    /// <summary>
    /// The git command that failed, e.g. "git push origin main".
    /// </summary>
    public string Command { get; } = command;

    public int ExitCode { get; } = exitCode;

    public string StandardError { get; } = standardError;
}
