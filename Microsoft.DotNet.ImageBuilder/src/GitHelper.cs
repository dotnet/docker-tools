// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public static class GitHelper
    {
        public static string GetCommitSha(string filePath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("git", $"log -1 --format=format:%h {filePath}");
            startInfo.RedirectStandardOutput = true;
            Process gitLogProcess = ExecuteHelper.Execute(
                startInfo, false, $"Unable to retrieve the commit for {filePath}");
            return gitLogProcess.StandardOutput.ReadToEnd().Trim();
        }
    }
}
