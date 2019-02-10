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
        private static Lazy<OS> _os = new Lazy<OS>(GetOS);

        public static Architecture Architecture => _architecture.Value;
        public static OS OS => _os.Value;

        private static string ExecuteCommand(
            string command, string errorMessage, string additionalArgs = null, bool isDryRun = false)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(
                "docker", $"{command} {additionalArgs}");
            startInfo.RedirectStandardOutput = true;
            Process process = ExecuteHelper.Execute(startInfo, isDryRun, errorMessage);
            return isDryRun ? "" : process.StandardOutput.ReadToEnd().Trim();
        }

        private static string ExecuteCommandWithFormat(
            string command, string outputFormat, string errorMessage, string additionalArgs = null, bool isDryRun = false) =>
            ExecuteCommand(command, errorMessage, $"{additionalArgs} -f \"{{{{ {outputFormat} }}}}\"", isDryRun);

        public static void ExecuteWithUser(Action action, string username, string password, string server, bool isDryRun)
        {
            bool userSpecified = username != null;
            if (userSpecified)
            {
                DockerHelper.Login(username, password, server, isDryRun);
            }

            try
            {
                action();
            }
            finally
            {
                if (userSpecified)
                {
                    DockerHelper.Logout(server, isDryRun);
                }
            }
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
                case "arm":
                case "arm_32":
                case "armv7l":
                    architecture = Architecture.ARM;
                    break;
                case "aarch64":
                case "arm64":
                    architecture = Architecture.ARM64;
                    break;
                default:
                    throw new PlatformNotSupportedException($"Unknown Docker Architecture '{infoArchitecture}'");
            }

            return architecture;
        }

        public static string GetImageDigest(string image, bool isDryRun)
        {
            return ExecuteCommandWithFormat(
                "inspect", "index .RepoDigests 0", "Failed to retrieve image digest", image, isDryRun);
        }

        private static OS GetOS()
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

            if (versionString.Contains('-'))
            {
                // Trim off the '-ce' or '-ee' suffix
                versionString = versionString.Substring(0, versionString.IndexOf('-'));
            }

            return Version.TryParse(versionString, out Version version) ? version : null;
        }

        public static long GetImageSize(string image, bool isDryRun)
        {
            string size = ExecuteCommandWithFormat(
                "inspect", ".Size", "Failed to retrieve image size", additionalArgs: image, isDryRun: isDryRun);
            return isDryRun ? 0 : long.Parse(size);
        }

        public static string GetRepo(string image)
        {
            return image.Substring(0, image.IndexOf(':'));
        }

        public static bool LocalImageExists(string tag, bool isDryRun) => ResourceExists(ManagementType.Image, tag, isDryRun);

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

        private static void Logout(string server, bool isDryRun)
        {
            ExecuteHelper.ExecuteWithRetry("docker", $"logout {server}", isDryRun);
        }

        public static void PullBaseImages(ManifestInfo manifest, Options options)
        {
            Logger.WriteHeading("PULLING LATEST BASE IMAGES");
            IEnumerable<string> baseImages = manifest.GetExternalFromImages().ToArray();
            if (baseImages.Any())
            {
                foreach (string fromImage in baseImages)
                {
                    PullImage(fromImage, options.IsDryRun);
                }
            }
            else
            {
                Logger.WriteMessage("No external base images to pull");
            }
        }

        public static void PullImage(string image, bool isDryRun)
        {
            ExecuteHelper.ExecuteWithRetry("docker", $"pull {image}", isDryRun);
        }

        public static string ReplaceRepo(string image, string newRepo)
        {
            return newRepo + image.Substring(image.IndexOf(':'));
        }

        private static bool ResourceExists(ManagementType type, string filterArg, bool isDryRun)
        {
            string output = ExecuteCommand(
                $"{Enum.GetName(typeof(ManagementType), type).ToLowerInvariant()} ls -a -q {filterArg}",
                "Failed to find resource",
                isDryRun: isDryRun);
            return output != "";
        }

        private enum ManagementType 
        {
            Image,
            Container,
        }  
    }
}
