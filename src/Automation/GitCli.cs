// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Executes git commands, masking the auth token in logs and error messages.
/// </summary>
internal sealed class GitCli(ILogger logger, string? secret)
{
    private const string SecretMask = "***";

    private readonly ILogger _logger = logger;

    // The secret can appear in both raw form and percent-escaped form (when embedded in a URL).
    private readonly string[] _secrets = string.IsNullOrEmpty(secret)
        ? []
        : [secret, Uri.EscapeDataString(secret)];

    /// <summary>
    /// Runs a git command and returns its result. The standard output is
    /// returned verbatim; call <see cref="GitCliResultExtensions.Trim"/> when
    /// trailing newlines should be stripped. Throws <see cref="GitException"/>
    /// on failure.
    /// </summary>
    public async Task<GitCliResult> RunAsync(string? workingDirectory, params string[] args)
    {
        (int exitCode, string output, string error) = await ExecuteAsync(workingDirectory, args);
        if (exitCode != 0)
        {
            throw new GitException(GetMaskedCommand(args), exitCode, Mask(error));
        }

        return new GitCliResult(output);
    }

    private async Task<(int ExitCode, string Output, string Error)> ExecuteAsync(
        string? workingDirectory, string[] args)
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

        _logger.LogInformation("Running '{Command}'", GetMaskedCommand(args));

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await outputTask, await errorTask);
    }

    private string GetMaskedCommand(string[] args) => Mask($"git {string.Join(' ', args)}");

    private string Mask(string text)
    {
        foreach (string secret in _secrets.Distinct())
        {
            text = text.Replace(secret, SecretMask);
        }

        return text;
    }
}
