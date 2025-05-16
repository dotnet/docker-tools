// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Manages an Azure Pipelines collapsible logging group.
/// Disposing of the object closes the logging group.
/// </summary>
/// <remarks>
/// See https://learn.microsoft.com/azure/devops/pipelines/scripts/logging-commands
/// </remarks>
internal sealed class LoggingGroup : IDisposable
{
    /// <summary>
    /// Creates a new collapsible logging group. When this object is created,
    /// all subsequent logging output will be inside this group until this
    /// object is disposed.
    /// </summary>
    /// <param name="groupName">The name of the logging group.</param>
    public LoggingGroup(string name)
    {
        Console.WriteLine($"##[group]{name}");
    }

    /// <summary>
    /// Ends the current collapsible logging group.
    /// </summary>
    public void Dispose()
    {
        Console.WriteLine($"##[endgroup]");
    }
}
