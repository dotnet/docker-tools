// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Newtonsoft.Json;
using Docker = Microsoft.DotNet.ImageBuilder.Models.Docker;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public static class DockerHelper
    {
        private static readonly Lazy<Architecture> s_architecture = new(GetArchitecture);
        private static readonly Lazy<OS> s_os = new(GetOS);

        public static Architecture Architecture => s_architecture.Value;
        public static OS OS => s_os.Value;

        public const string DockerHubRegistry = "docker.io";
        public const string DockerHubApiRegistry = "registry-1.docker.io";
        public const string AcrDomain = ".azurecr.io";

        public static string FormatAcrName(string acrName)
        {
            if (!acrName.EndsWith(AcrDomain))
            {
                acrName = $"{acrName}{AcrDomain}";
            }

            return acrName;
        }

        public static Uri GetAcrUri(string acrName) => new($"https://{FormatAcrName(acrName)}");

        public static void ExecuteWithUser(Action action, string? username, string? password, string? server, bool isDryRun)
        {
            ExecuteWithUserAsync(() =>
            {
                action();
                return Task.CompletedTask;
            },
            username, password, server, isDryRun).GetAwaiter().GetResult();
        }

        public static async Task ExecuteWithUserAsync(Func<Task> action, string? username, string? password, string? server, bool isDryRun)
        {
            bool loggedIn = false;
            if (username is not null && password is not null && server is not null)
            {
                DockerHelper.Login(username, password, server, isDryRun);
                loggedIn = true;
            }

            try
            {
                await action();
            }
            finally
            {
                if (loggedIn && server is not null)
                {
                    DockerHelper.Logout(server, isDryRun);
                }
            }
        }

        public static IEnumerable<string> GetImageDigests(string image, bool isDryRun)
        {
            string digests = ExecuteCommandWithFormat(
                "inspect", "index .RepoDigests", "Failed to retrieve image digests", image, isDryRun);

            string trimmedDigests = digests.TrimStart('[').TrimEnd(']');
            if (trimmedDigests == string.Empty)
            {
                return Enumerable.Empty<string>();
            }

            return trimmedDigests.Split(' ');
        }

        public static long GetImageSize(string image, bool isDryRun)
        {
            string size = ExecuteCommandWithFormat(
                "inspect", ".Size", "Failed to retrieve image size", additionalArgs: image, isDryRun: isDryRun);
            return isDryRun ? 0 : long.Parse(size);
        }

        public static string GetRepo(string image)
        {
            int tagSeparator = GetTagOrDigestSeparatorIndex(image);
            if (tagSeparator >= 0)
            {
                return image.Substring(0, tagSeparator);
            }

            return image;
        }

        public static bool LocalImageExists(string tag, bool isDryRun) => ResourceExists(ManagementType.Image, tag, isDryRun);

        public static void Login(string username, string password, string server, bool isDryRun)
        {
            Version? clientVersion = GetClientVersion();
            if (clientVersion >= new Version(17, 7))
            {
                ProcessStartInfo startInfo = new(
                    "docker", $"login -u {username} --password-stdin {server}")
                {
                    RedirectStandardInput = true
                };
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

        public static void PullImage(string image, string? platform, bool isDryRun)
        {
            string platformArg = "";
            if (platform is not null)
            {
                platformArg = $"--platform {platform} ";
            }

            ExecuteHelper.ExecuteWithRetry("docker", $"pull {platformArg}{image}", isDryRun);
        }

        public static string ReplaceRepo(string image, string newRepo) =>
            newRepo + image.Substring(GetTagOrDigestSeparatorIndex(image));

        public static void CreateTag(string image, string tag, bool isDryRun)
        {
            DockerHelper.ExecuteCommand("tag", "Failed to create tag", $"{image} {tag}", isDryRun);
        }

        public static string GetCreatedDate(string image, bool isDryRun)
        {
            return ExecuteCommandWithFormat(
                "inspect", ".Created", "Failed to retrieve created date", image, isDryRun);
        }

        public static string GetDigestSha(string digest) => digest.Substring(digest.IndexOf("@") + 1);

        public static string GetDigestString(string repo, string sha) => $"{repo}@{sha}";

        public static string GetImageName(string? registry, string repo, string? tag = null, string? digest = null)
        {
            if (tag != null && digest != null)
            {
                throw new InvalidOperationException($"Invalid to provide both the {nameof(tag)} and {nameof(digest)} arguments.");
            }

            string imageName = registry ?? string.Empty;
            if (imageName.Length > 0)
            {
                imageName += $"/";
            }

            imageName += repo;

            if (tag != null)
            {
                return $"{imageName}:{tag}";
            }
            else if (digest != null)
            {
                return $"{imageName}@{digest}";
            }

            return imageName;
        }

        public static string GetTagName(string imageName) => imageName[(GetTagOrDigestSeparatorIndex(imageName) + 1)..];

        public static string NormalizeRepo(string image)
        {
            string? registry = GetRegistry(image);
            string repoAndTag = TrimRegistry(image, registry);

            if ((registry is null || registry == DockerHubRegistry) && !repoAndTag.Contains('/'))
            {
                repoAndTag = $"library/{repoAndTag}";
            }

            if (registry is null)
            {
                return repoAndTag;
            }

            return $"{registry}/{repoAndTag}";
        }

        public static string TrimRegistry(string tag) => TrimRegistry(tag, GetRegistry(tag));

        public static string TrimRegistry(string tag, string? registry) => tag.TrimStart($"{registry}/");

        public static bool IsInRegistry(string tag, string registry) => registry is not null && tag.StartsWith(registry);

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

        public static string? GetRegistry(string imageName)
        {
            int separatorIndex = imageName.IndexOf("/");
            if (separatorIndex >= 0)
            {
                string firstSegment = imageName.Substring(0, separatorIndex);
                if (firstSegment.Contains(".") || firstSegment.Contains(":"))
                {
                    return firstSegment;
                }
            }
            

            return null;
        }

        private static int GetTagOrDigestSeparatorIndex(string imageName)
        {
            int separatorPosition = imageName.IndexOf('@');
            if (separatorPosition < 0)
            {
                separatorPosition = imageName.IndexOf(':');
            }

            return separatorPosition;
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

        private static Version? GetClientVersion()
        {
            // Docker version string format - <major>.<minor>.<patch>-[ce,ee]
            string versionString = ExecuteCommandWithFormat("version", ".Client.Version", "Failed to retrieve Docker version");

            if (versionString.Contains('-'))
            {
                // Trim off the '-ce' or '-ee' suffix
                versionString = versionString.Substring(0, versionString.IndexOf('-'));
            }

            return Version.TryParse(versionString, out Version? version) ? version : null;
        }

        private static void Logout(string server, bool isDryRun)
        {
            ExecuteHelper.ExecuteWithRetry("docker", $"logout {server}", isDryRun);
        }

        private static bool ResourceExists(ManagementType type, string filterArg, bool isDryRun)
        {
            string output = ExecuteCommand(
                $"{Enum.GetName(typeof(ManagementType), type)?.ToLowerInvariant()} ls -a -q {filterArg}",
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

        public static string ExecuteCommand(
            string command, string errorMessage, string? additionalArgs = null, bool isDryRun = false)
        {
            string output = ExecuteHelper.Execute("docker", $"{command} {additionalArgs}", isDryRun, errorMessage);
            return isDryRun ? "" : output;
        }

        private static string ExecuteCommandWithFormat(
            string command, string outputFormat, string errorMessage, string? additionalArgs = null, bool isDryRun = false) =>
            ExecuteCommand(command, errorMessage, $"{additionalArgs} -f \"{{{{ {outputFormat} }}}}\"", isDryRun);

        private enum ManagementType
        {
            Image,
            Container,
        }
    }
}
#nullable disable
