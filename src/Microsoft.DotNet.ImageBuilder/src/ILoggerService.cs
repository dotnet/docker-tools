// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder
{
    public interface ILoggerService
    {
        void WriteError(string error);
        void WriteHeading(string heading);
        void WriteMessage(string? message = null);
        void WriteSubheading(string subheading);
        void WriteWarning(string message);
        void WriteDebug(string message);
        void WriteCommand(string command);
    }
}
