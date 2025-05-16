// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel.Composition;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(ILoggerService))]
    internal class LoggerService : ILoggerService
    {
        /// <inheritdoc />
        public void WriteError(string error)
        {
            Logger.WriteError(error);
        }

        /// <inheritdoc />
        public void WriteHeading(string heading)
        {
            Logger.WriteHeading(heading);
        }

        /// <inheritdoc />
        public void WriteMessage(string? message = null)
        {
            Logger.WriteMessage(message);
        }

        /// <inheritdoc />
        public void WriteSubheading(string subheading)
        {
            Logger.WriteSubheading(subheading);
        }

        /// <inheritdoc />
        public void WriteCommand(string command)
        {
            Logger.WriteCommand(command);
        }

        /// <inheritdoc />
        public void WriteWarning(string message)
        {
            Logger.WriteWarning(message);
        }

        /// <inheritdoc />
        public void WriteDebug(string message)
        {
            Logger.WriteDebug(message);
        }

        /// <inheiritdoc />
        public IDisposable LogGroup(string name)
        {
            return new LoggingGroup(name, this);
        }
    }
}
