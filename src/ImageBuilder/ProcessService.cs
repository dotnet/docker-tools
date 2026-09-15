// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.ImageBuilder
{
    public class ProcessService : IProcessService
    {
        public string? Execute(
            string fileName, string args, bool isDryRun, CancellationToken cancellationToken, string? errorMessage = null, string? executeMessageOverride = null) =>
            ExecuteHelper.Execute(fileName, args, isDryRun, cancellationToken, errorMessage, executeMessageOverride);

        public string? Execute(
            ProcessStartInfo info, bool isDryRun, CancellationToken cancellationToken, string? errorMessage = null, string? executeMessageOverride = null) =>
            ExecuteHelper.Execute(info, isDryRun, cancellationToken, errorMessage, executeMessageOverride);
    }
}
