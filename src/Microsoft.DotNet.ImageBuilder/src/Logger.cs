// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Microsoft.DotNet.ImageBuilder;

public static class Logger
{
    public static void WriteError(string error)
    {
        Console.Error.WriteLine($"##[error]{error}");
    }

    public static void WriteHeading(string heading)
    {
        Console.WriteLine();
        Console.WriteLine(heading);
        Console.WriteLine(new string('-', heading.Length));
    }

    public static void WriteMessage(string? message = null)
    {
        Console.WriteLine(message);
    }

    public static void WriteSubheading(string subheading)
    {
        WriteMessage($"##[section]{subheading}");
    }

    public static void WriteCommand(string command)
    {
        WriteMessage($"##[command]{command}");
    }

    public static void WriteWarning(string message)
    {
        WriteMessage($"##[warning]{message}");
    }

    public static void WriteDebug(string message)
    {
        WriteMessage($"##[debug]{message}");
    }
}
