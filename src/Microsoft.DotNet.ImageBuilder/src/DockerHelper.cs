// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Docker = Microsoft.DotNet.ImageBuilder.Models.Docker;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class DockerHelper
    {
        private static readonly Lazy<Architecture> s_architecture = new Lazy<Architecture>(GetArchitecture);
        private static readonly Lazy<OS> s_os = new Lazy<OS>(GetOS);

        public static Architecture Architecture => s_architecture.Value;
        public static OS OS => s_os.Value;

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

        public static string GetImageDigest(string image, bool isDryRun)
        {
            try
            {
                return ExecuteCommandWithFormat(
                    "inspect", "index .RepoDigests 0", "Failed to retrieve image digest", image, isDryRun);
            }
            // A digest will not exist for images that have been built locally or have been manually installed
            catch (InvalidOperationException e) when (e.Message.Contains("index out of range"))
            {
                return null;
            }
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
                ExecuteHelper.ExecuteWithRetry(
                    startInfo,
                    process =>
                    {
                        process.StandardInput.WriteLine(password);
                        process.StandardInput.Close();
                    },
                    isDryRun);
            }
            else
            {
                ExecuteHelper.ExecuteWithRetry(
                    "docker",
                    $"login -u {username} -p {password} {server}",
                    isDryRun,
                    executeMessageOverride: $"login -u {username} -p ******** {server}");
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

        public static string GetImageArch(string image, bool isDryRun)
        {
            return ExecuteCommandWithFormat(
                "inspect", ".Architecture", "Failed to retrieve image architecture", additionalArgs: image, isDryRun: isDryRun);
        }

        public static void SaveImage(string image, string tarFilePath, bool isDryRun)
        {
            DockerHelper.ExecuteCommand("save", "Failed to save image", $"-o {tarFilePath} {image}", isDryRun);
        }

        public static void LoadImage(string tarFilePath, bool isDryRun)
        {
            DockerHelper.ExecuteCommand("load", "Failed to load image", $"-i {tarFilePath}", isDryRun);
        }

        public static void CreateTag(string image, string tag, bool isDryRun)
        {
            DockerHelper.ExecuteCommand("tag", "Failed to create tag", $"{image} {tag}", isDryRun);
        }

        public static string GetCreatedDate(string image, bool isDryRun)
        {
            return ExecuteCommandWithFormat(
                "inspect", ".Created", "Failed to retrieve created date", image, isDryRun);
        }

        public static string GetDigestSha(string digest) => digest?.Substring(digest.IndexOf("@") + 1);

        public static string GetDigestString(string repo, string sha) => $"{repo}@{sha}";

        /// <remarks>
        /// This method depends on the experimental Docker CLI `manifest` command.  As a result, this method
        /// should only used for developer usage scenarios.
        /// </remarks>
        public static Docker.Manifest InspectManifest(string image, bool isDryRun)
        {
            string manifest = ExecuteCommand(
                "manifest inspect", "Failed to inspect manifest", $"{image} --verbose", isDryRun);
            return JsonConvert.DeserializeObject<Docker.Manifest>(manifest);
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

        private static void Logout(string server, bool isDryRun)
        {
            ExecuteHelper.ExecuteWithRetry("docker", $"logout {server}", isDryRun);
        }

        private static bool ResourceExists(ManagementType type, string filterArg, bool isDryRun)
        {
            string output = ExecuteCommand(
                $"{Enum.GetName(typeof(ManagementType), type).ToLowerInvariant()} ls -a -q {filterArg}",
                "Failed to find resource",
                isDryRun: isDryRun);
            return output != "";
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

        private static string ExecuteCommand(
            string command, string errorMessage, string additionalArgs = null, bool isDryRun = false)
        {
            string output = ExecuteHelper.Execute("docker", $"{command} {additionalArgs}", isDryRun, errorMessage);
            return isDryRun ? "" : output;
        }

        private static string ExecuteCommandWithFormat(
            string command, string outputFormat, string errorMessage, string additionalArgs = null, bool isDryRun = false) =>
            ExecuteCommand(command, errorMessage, $"{additionalArgs} -f \"{{{{ {outputFormat} }}}}\"", isDryRun);

        private enum ManagementType
        {
            Image,
            Container,
        }
    }
}
