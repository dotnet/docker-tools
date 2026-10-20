// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.ImageBuilder;

internal class EnvironmentService : IEnvironmentService
{
    public void ExitProcess(int exitCode) => Environment.Exit(exitCode);

    public int ExitCode
    {
        get => Environment.ExitCode;
        set => Environment.ExitCode = value;
    }

    public string? GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);
}
