// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

public interface IEnvironmentService
{
    void Exit(int exitCode);

    /// <summary>
    /// Gets or sets the exit code for the process, allowing graceful shutdown.
    /// </summary>
    int ExitCode { get; set; }

    string? GetEnvironmentVariable(string variable);
}
