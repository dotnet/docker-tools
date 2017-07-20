// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Diagnostics;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class DockerHelper
    {
        public static Architecture GetArchitecture()
        {
            Architecture architecture;

            string infoArchitecture = ExecuteCommand("info", ".Architecture", "Failed to detect Docker architecture");
            switch (infoArchitecture)
            {
                case "x86_64":
                    architecture = Architecture.AMD64;
                    break;
                case "arm_32":
                    architecture = Architecture.ARM;
                    break;
                default:
                    throw new PlatformNotSupportedException("Unknown Docker Architecture");
            }

            return architecture;
        }

        public static string GetOS()
        {
            return ExecuteCommand("version", ".Server.Os", "Failed to detect Docker OS");
        }

        private static string ExecuteCommand(string command, string outputFormat, string errorMessage)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("docker", $"{command} -f \"{{{{ {outputFormat} }}}}\"");
            startInfo.RedirectStandardOutput = true;
            Process process = ExecuteHelper.Execute(startInfo, false, errorMessage);
            return process.StandardOutput.ReadToEnd().Trim();
        }
    }
}
