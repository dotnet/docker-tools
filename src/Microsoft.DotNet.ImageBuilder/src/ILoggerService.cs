// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder
{
    public interface ILoggerService
    {
        void WriteError(string error);
        void WriteHeading(string heading);
        void WriteMessage();
        void WriteMessage(string message);
        void WriteSubheading(string subheading);
    }
}
