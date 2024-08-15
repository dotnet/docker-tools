// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace FilePusher;

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

    private static string? EscapeVsoDirectives(string? message)
    {
        return message?.Replace("##vso", "#VSO_DIRECTIVE");
    }
}
