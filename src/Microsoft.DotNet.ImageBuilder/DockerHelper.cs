// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Diagnostics;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class DockerHelper
    {
        private static Lazy<Architecture> _architecture = new Lazy<Architecture>(GetArchitecture);

        public static Architecture Architecture => _architecture.Value;

        public static void Login(string username, string password, bool isDryRun)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(
                "docker", $"login -u {username} --password-stdin");
            startInfo.RedirectStandardInput = true;
            ExecuteHelper.Execute(
                startInfo,
                info => {
                    Process process = Process.Start(info);
                    process.StandardInput.WriteLine(password);
                    process.StandardInput.Close();
                    process.WaitForExit();
                    return process;
                },
                isDryRun);
        }

        private static Architecture GetArchitecture()
        {
            Architecture architecture;

            string infoArchitecture = ExecuteCommandWithFormat(
                "info", ".Architecture", "Failed to detect Docker architecture");
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

        public static string GetImageDigest(string image, bool isDryRun)
        {
            return ExecuteCommandWithFormat(
                "inspect", "index .RepoDigests 0", "Failed to retrieve image digest", image, isDryRun);
        }

        public static string GetOS()
        {
            return ExecuteCommandWithFormat("version", ".Server.Os", "Failed to detect Docker OS");
        }

        public static void PullBaseImages(ManifestInfo manifest, Options options)
        {
            Utilities.WriteHeading("PULLING LATEST BASE IMAGES");
            foreach (string fromImage in manifest.GetExternalFromImages())
            {
                ExecuteHelper.ExecuteWithRetry("docker", $"pull {fromImage}", options.IsDryRun);
            }
        }

        private static string ExecuteCommandWithFormat(
            string command, string outputFormat, string errorMessage, string additionalArgs = null, bool isDryRun = false)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(
                "docker", $"{command} -f \"{{{{ {outputFormat} }}}}\" {additionalArgs}");
            startInfo.RedirectStandardOutput = true;
            Process process = ExecuteHelper.Execute(startInfo, isDryRun, errorMessage);
            return isDryRun ? "" : process.StandardOutput.ReadToEnd().Trim();
        }

        public static string ReplaceImageOwner(string image, string newOwner)
        {
            return newOwner + image.Substring(image.IndexOf('/'));
        }
    }
}
