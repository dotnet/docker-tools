// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// The result of running a git command via <see cref="GitCli"/>.
/// </summary>
/// <param name="StandardOutput">The command's standard output, verbatim.</param>
internal readonly record struct GitCliResult(string StandardOutput);

internal static class GitCliResultExtensions
{
    /// <summary>
    /// Returns the command's standard output with trailing carriage returns and
    /// newlines removed. Use this for commands whose output is a single value
    /// (such as a SHA or branch name), where the trailing newline git appends is
    /// not part of the content.
    /// </summary>
    public static string Trim(this GitCliResult result) =>
        result.StandardOutput.TrimEnd('\r', '\n');
}
