// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Microsoft.DotNet.ImageBuilder;

public interface ILoggerService
{
    /// <summary>
    /// Writes an error message to the log.
    /// </summary>
    /// <param name="error">The error message to log.</param>
    void WriteError(string error);

    /// <summary>
    /// Writes a heading to the log, typically used for major sections or
    /// operations.
    /// </summary>
    /// <param name="heading">The heading text to log.</param>
    void WriteHeading(string heading);

    /// <summary>
    /// Writes a general message to the log.
    /// </summary>
    /// <param name="message">The message to log. Can be null.</param>
    void WriteMessage(string? message = null);

    /// <summary>
    /// Writes a subheading to the log, typically used for sub-sections within
    /// a headed section.
    /// </summary>
    /// <param name="subheading">The subheading text to log.</param>
    void WriteSubheading(string subheading);

    /// <summary>
    /// Writes a warning message to the log.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    void WriteWarning(string message);

    /// <summary>
    /// Writes a debug message to the log that might only be shown in verbose
    /// logging modes.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    void WriteDebug(string message);

    /// <summary>
    /// Writes a command execution message to the log. This does not actually
    /// execute the command or capture its output. It is intended to record
    /// that a command was executed.
    /// </summary>
    /// <param name="command">
    /// The command to log, including executable name and all arguments
    /// </param>
    void WriteCommand(string command);

    /// <summary>
    /// Creates a logical grouping of log messages. Useful for grouping large
    /// amounts of related text, like executable/process output.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <returns>An IDisposable that when disposed will end the group.</returns>
    IDisposable LogGroup(string name);
}
