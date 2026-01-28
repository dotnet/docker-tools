#nullable disable
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


namespace Microsoft.DotNet.ImageBuilder
{
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

        public void WriteMessage()
        {
            Logger.WriteMessage();
        }

        public void WriteMessage(string message)
        {
            Logger.WriteMessage(message);
        }

        public void WriteSubheading(string subheading)
        {
            Logger.WriteSubheading(subheading);
        }
    }
}
