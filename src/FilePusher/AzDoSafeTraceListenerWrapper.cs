// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.DotNet.DockerTools.FilePusher;

public class AzDoSafeTraceListenerWrapper(TraceListener innerTraceListener) : TraceListener
{
    private readonly TraceListener _innerTraceListener = innerTraceListener;

    public override void Write(string? message)
    {
        _innerTraceListener.Write(EscapeVsoDirectives(message));
    }

    public override void WriteLine(string? message)
    {
        _innerTraceListener.WriteLine(EscapeVsoDirectives(message));
    }

    /// <summary>
    /// This method "escapes" Azure DevOps Pipeline tasks/variable assignments in strings so that they are safe to
    /// output to the console in pipeline.
    /// This prevents issues like https://github.com/dotnet/docker-tools/issues/1388, where pushing files containing
    /// these AzDO variable assignments results in pipeline failure.
    /// Azure DevOps documentation: https://learn.microsoft.com/en-us/azure/devops/pipelines/process/set-variables-scripts
    /// </summary>
    private static string? EscapeVsoDirectives(string? message)
    {
        return message?.Replace("##vso", "#VSO_DIRECTIVE");
    }
}
