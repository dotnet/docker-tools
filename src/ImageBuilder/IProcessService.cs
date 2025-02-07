// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder
{
    public interface IProcessService
    {
        string? Execute(string fileName, string args, bool isDryRun, string? errorMessage = null, string? executeMessageOverride = null);

        string? Execute(ProcessStartInfo info, bool isDryRun, string? errorMessage = null, string? executeMessageOverride = null);
    }
}
#nullable disable
