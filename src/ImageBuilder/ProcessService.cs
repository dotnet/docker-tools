﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.Composition;
using System.Diagnostics;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IProcessService))]
    public class ProcessService : IProcessService
    {
        public string? Execute(
            string fileName, string args, bool isDryRun, string? errorMessage = null, string? executeMessageOverride = null) =>
            ExecuteHelper.Execute(fileName, args, isDryRun, errorMessage, executeMessageOverride);

        public string? Execute(
            ProcessStartInfo info, bool isDryRun, string? errorMessage = null, string? executeMessageOverride = null) =>
            ExecuteHelper.Execute(info, isDryRun, errorMessage, executeMessageOverride);
    }
}
#nullable disable
