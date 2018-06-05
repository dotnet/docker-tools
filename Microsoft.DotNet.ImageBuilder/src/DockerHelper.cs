// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
                case "armv7l":
                    architecture = Architecture.ARM;
                    break;
                default:
                    throw new PlatformNotSupportedException("Unknown Docker Architecture '$(infoArchitecture)'");
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
            return Version.TryParse(versionString, out Version version) ? version : null;
        }

        public static string GetImageOwner(string image)
        {
            return image.Substring(0, image.IndexOf('/'));
        }

        public static string GetRepo(string image)
        {
            return image.Substring(0, image.IndexOf(':'));
        }

        public static void Login(string username, string password, string server, bool isDryRun)
        {
            Version clientVersion = GetClientVersion();
            if (clientVersion >= new Version(17, 7))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(
                    "docker", $"login -u {username} --password-stdin {server}");
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
                ExecuteHelper.Execute(
                    "docker",
                    $"login -u {username} -p {password} {server}",
                    isDryRun,
                    executeMessageOverride: $"login -u {username} -p ******** {server}");
            }
        }

        public static void Logout(string server, bool isDryRun)
        {
            ExecuteHelper.Execute("docker", $"logout {server}", isDryRun);
        }

        public static void PullBaseImages(ManifestInfo manifest, Options options)
        {
            Logger.WriteHeading("PULLING LATEST BASE IMAGES");
            IEnumerable<string> baseImages = manifest.GetExternalFromImages().ToArray();
            if (baseImages.Any())
            {
                foreach (string fromImage in baseImages)
                {
                    ExecuteHelper.ExecuteWithRetry("docker", $"pull {fromImage}", options.IsDryRun);
                }
            }
            else
            {
                Logger.WriteMessage("No external base images to pull");
            }
        }

        public static string ReplaceRepo(string image, string newRepo)
        {
            return newRepo + GetRepo(image);
        }
    }
}
