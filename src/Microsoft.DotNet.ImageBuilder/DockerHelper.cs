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

        private static string ExecuteCommandWithFormat(
            string command, string outputFormat, string errorMessage, string additionalArgs = null, bool isDryRun = false)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(
                "docker", $"{command} -f \"{{{{ {outputFormat} }}}}\" {additionalArgs}");
            startInfo.RedirectStandardOutput = true;
            Process process = ExecuteHelper.Execute(startInfo, isDryRun, errorMessage);
            return isDryRun ? "" : process.StandardOutput.ReadToEnd().Trim();
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

        public static OS GetOS()
        {
            string osString = ExecuteCommandWithFormat("version", ".Server.Os", "Failed to detect Docker OS");
            if (!Enum.TryParse(osString, true, out OS os))
            {
                throw new PlatformNotSupportedException("Unknown Docker OS");
            }

            return os;
        }

        private static Version GetClientVersion()
        {
            // Docker version string format - <major>.<minor>.<patch>-[ce,ee]
            string versionString = ExecuteCommandWithFormat("version", ".Client.Version", "Failed to retrieve Docker version");

            // Trim off the '-ce' or '-ee' suffix
            versionString = versionString.Substring(0, versionString.IndexOf('-'));
            return Version.Parse(versionString);
        }

        public static void Login(string username, string password, bool isDryRun)
        {
            Version clientVersion = GetClientVersion();
            if (clientVersion >= new Version(17, 7))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(
                    "docker", $"login -u {username} --password-stdin");
                startInfo.RedirectStandardInput = true;
                ExecuteHelper.Execute(
                    startInfo,
                    info =>
                    {
                        Process process = Process.Start(info);
                        process.StandardInput.WriteLine(password);
                        process.StandardInput.Close();
                        process.WaitForExit();
                        return process;
                    },
                    isDryRun);
            }
            else
            {
                string loginArgsWithoutPassword = $"login -u {username} -p";
                ExecuteHelper.Execute(
                    "docker",
                    $"{loginArgsWithoutPassword} {password}",
                    isDryRun,
                    executeMessageOverride: $"{loginArgsWithoutPassword} ********");
            }
        }

        public static void PullBaseImages(ManifestInfo manifest, Options options)
        {
            Utilities.WriteHeading("PULLING LATEST BASE IMAGES");
            foreach (string fromImage in manifest.GetExternalFromImages())
            {
                ExecuteHelper.ExecuteWithRetry("docker", $"pull {fromImage}", options.IsDryRun);
            }
        }

        public static string ReplaceImageOwner(string image, string newOwner)
        {
            return newOwner + image.Substring(image.IndexOf('/'));
        }
    }
}
