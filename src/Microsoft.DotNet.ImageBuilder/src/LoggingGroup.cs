// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Manages an Azure Pipelines collapsible logging group.
/// Disposing of the object closes the logging group.
/// See https://learn.microsoft.com/azure/devops/pipelines/scripts/logging-commands
/// </summary>
/// <remarks>
/// Only one LoggingGroup can be open at a time. If a second group is created
/// without closing the first, then the first group will not be collapsible.
/// </remarks>
internal sealed class LoggingGroup : IDisposable
{
    private readonly ILoggerService _logger;

    /// <summary>
    /// Creates a new collapsible logging group. When this object is created,
    /// all subsequent logging output will be inside this group until this
    /// object is disposed.
    /// </summary>
    /// <param name="groupName">The name of the logging group</param>
    /// <param name="loggerService">The logger service to use for output</param>
    public LoggingGroup(string name, ILoggerService loggerService)
    {
        _logger = loggerService;
        _logger.WriteMessage($"##[group]{name}");
    }

    /// <summary>
    /// Ends the current collapsible logging group.
    /// </summary>
    public void Dispose()
    {
        _logger.WriteMessage($"##[endgroup]");
    }
}
