// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.ComponentModel.Composition;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(ILoggerService))]
    internal class LoggerService : ILoggerService
    {
        public void WriteError(string error)
        {
            Logger.WriteError(error);
        }

        public void WriteHeading(string heading)
        {
            Logger.WriteHeading(heading);
        }

        public void WriteMessage(string? message = null)
        {
            Logger.WriteMessage(message);
        }

        public void WriteSubheading(string subheading)
        {
            Logger.WriteSubheading(subheading);
        }

        public void WriteCommand(string command)
        {
            Logger.WriteCommand(command);
        }

        public void WriteWarning(string message)
        {
            Logger.WriteWarning(message);
        }

        public void WriteDebug(string message)
        {
            Logger.WriteDebug(message);
        }
    }
}
