// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using static System.Console;

namespace Microsoft.DotNet.ImageBuilder
{
    /// <summary>
    /// Helper class for logging messages to the console in a format that is compatible with Azure Pipelines.
    /// https://learn.microsoft.com/en-us/azure/devops/pipelines/scripts/logging-commands?view=azure-devops&tabs=bash#logging-command-format
    /// </summary>
    public static class Logger
    {
        // Keep track of whether a logging group is open so we can close it when creating a new group.

        /// <summary>
        /// Writes an empty line to the console.
        /// </summary>
        public static void WriteMessage() =>
            WriteLine();

        /// <summary>
        /// Writes a message to the console.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public static void WriteMessage(string message) =>
            WriteLine(message);

        /// <summary>
        /// Writes a warning message to the console using Azure Pipelines warning format.
        /// </summary>
        /// <param name="message">The warning message to write.</param>
        public static void WriteWarning(string message) =>
            WriteLine($"##[warning]{message}");

        /// <summary>
        /// Writes an error message to the error output using Azure Pipelines error format.
        /// </summary>
        /// <param name="message">The error message to write.</param>
        public static void WriteError(string message) =>
            Error.WriteLine($"##[error]{message}");

        /// <summary>
        /// Write a message to the log with a group heading. This is used to group related messages together in the log
        /// output. In Azure Pipelines, this will create a collapsible group in the log output.
        /// </summary>
        /// <remarks>
        /// Follow up with a call to EndGroup to close the group.
        /// </remarks>
        /// <param name="message">The group heading message.</param>
        public static void WriteGroup(string message)
        {
            WriteLine();
            WriteLine($"##[group]{message}");
            WriteLine();
        }

        /// <summary>
        /// Ends a group in the log output with the specified message.
        /// </summary>
        /// <remarks>
        /// This should only be called after WriteGroup to close the current group.
        /// </remarks>
        public static void EndGroup() => WriteLine($"##[endgroup]");

        /// <summary>
        /// Writes a section header to the log output using Azure Pipelines section format.
        /// </summary>
        /// <param name="message">The section header message.</param>
        public static void WriteSection(string message) => WriteLine($"##[section]{message}");

        /// <summary>
        /// Writes a debug message to the log output using Azure Pipelines debug format.
        /// </summary>
        /// <param name="message">The debug message to write.</param>
        public static void WriteDebug(string message) => WriteLine($"##[debug]{message}");

        /// <summary>
        /// Writes a command message to the log output using Azure Pipelines command format.
        /// </summary>
        /// <param name="message">The command message to write.</param>
        public static void WriteCommand(string message) => WriteLine($"##[command]{message}");
    }
}
