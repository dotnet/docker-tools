// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.DotNet.Automation.Tests;

/// <summary>
/// Runs the real git CLI for test setup and assertions. Mirrors how the
/// library itself shells out to git, so scenarios exercise genuine git
/// behavior rather than a simulation.
/// </summary>
internal static class GitRunner
{
    public static async Task<string> RunAsync(string? workingDirectory, params string[] args)
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

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        string output = await outputTask;
        string error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed with exit code {process.ExitCode}.{Environment.NewLine}{error}");
        }

        return output;
    }
}
