// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder
{
    public class DockerService : IDockerService
    {
        private const string BuildSecretEnvironmentVariablePrefix = "IMAGEBUILDER_BUILD_SECRET_";

        public Architecture Architecture => DockerHelper.Architecture;

        public void PullImage(string image, string? platform, bool isDryRun) => DockerHelper.PullImage(image, platform, isDryRun);

        public void PushImage(string tag, bool isDryRun) => ExecuteHelper.ExecuteWithRetry("docker", $"push {tag}", isDryRun);

        public void PushManifestList(string manifestListTag, bool isDryRun) =>
            ExecuteHelper.ExecuteWithRetry("docker", $"manifest push {manifestListTag}", isDryRun);

        public void CreateTag(string image, string tag, bool isDryRun) => DockerHelper.CreateTag(image, tag, isDryRun);

        public void CreateManifestList(string manifestListTag, IEnumerable<string> images, bool isDryRun) =>
            // Use the --amend option to handle potential retries: https://github.com/dotnet/docker-tools/issues/1098
            ExecuteHelper.ExecuteWithRetry(
                "docker", $"manifest create --amend {manifestListTag} {string.Join(' ', images.ToArray())}", isDryRun);

        public string? BuildImage(
            string dockerfilePath,
            string buildContextPath,
            string platform,
            IEnumerable<string> tags,
            IDictionary<string, string?> buildArgs,
            IReadOnlyDictionary<string, string> buildSecrets,
            BuildSecretMode buildSecretMode,
            IEnumerable<string> dockerBuildOptions,
            bool isRetryEnabled,
            bool isDryRun)
        {
            List<string> dockerArgs = ["build", "--platform", platform];
            ProcessStartInfo processStartInfo = new("docker");

            foreach (string tag in tags)
            {
                dockerArgs.Add("-t");
                dockerArgs.Add(tag);
            }

            dockerArgs.Add("-f");
            dockerArgs.Add(dockerfilePath);

            List<string> buildSecretArgs = buildSecretMode switch
            {
                BuildSecretMode.SecretMounts => GetSecretMountArgs(processStartInfo, buildSecrets),
                BuildSecretMode.BuildArgs => GetSecretBuildArgs(buildSecrets),
                _ => throw new ArgumentOutOfRangeException(nameof(buildSecretMode), buildSecretMode, null),
            };
            dockerArgs.AddRange(buildSecretArgs);

            foreach (KeyValuePair<string, string?> buildArg in buildArgs)
            {
                dockerArgs.Add("--build-arg");
                dockerArgs.Add($"{buildArg.Key}={buildArg.Value}");
            }

            dockerBuildOptions = dockerBuildOptions.Where(option => !string.IsNullOrWhiteSpace(option));
            dockerArgs.AddRange(dockerBuildOptions);
            dockerArgs.Add(buildContextPath);

            processStartInfo.Arguments = string.Join(' ', dockerArgs);

            if (isRetryEnabled)
            {
                return ExecuteHelper.ExecuteWithRetry(processStartInfo, isDryRun: isDryRun);
            }
            else
            {
                return ExecuteHelper.Execute(processStartInfo, isDryRun);
            }
        }

        private static List<string> GetSecretMountArgs(
            ProcessStartInfo processStartInfo,
            IReadOnlyDictionary<string, string> buildSecrets)
        {
            List<string> buildSecretArgs = [];
            int secretNumber = 0;
            foreach (KeyValuePair<string, string> buildSecret in buildSecrets)
            {
                // https://docs.docker.com/build/building/secrets/
                string environmentVariableName = $"{BuildSecretEnvironmentVariablePrefix}{secretNumber}";
                buildSecretArgs.Add("--secret");
                buildSecretArgs.Add($"id={buildSecret.Key},env={environmentVariableName}");
                processStartInfo.Environment[environmentVariableName] = buildSecret.Value;
                secretNumber++;
            }

            return buildSecretArgs;
        }

        private static List<string> GetSecretBuildArgs(IReadOnlyDictionary<string, string> buildSecrets)
        {
            List<string> buildSecretArgs = [];
            foreach (KeyValuePair<string, string> buildSecret in buildSecrets)
            {
                buildSecretArgs.Add("--build-arg");
                buildSecretArgs.Add($"{buildSecret.Key}={buildSecret.Value}");
            }

            return buildSecretArgs;
        }

        public (Architecture Arch, string? Variant) GetImageArch(string image, bool isDryRun)
        {
            string archAndVariant = DockerHelper.ExecuteCommand(
                "inspect", "Failed to retrieve image architecture", $"-f \"{{{{ .Architecture }}}}/{{{{ .Variant }}}}\" {image}", isDryRun);
            string[] parts = archAndVariant.Split('/', StringSplitOptions.RemoveEmptyEntries);
            Architecture arch = Enum.Parse<Architecture>(parts[0], ignoreCase: true);
            string? variant = parts.Length > 1 ? parts[1] : null;
            return (arch, variant);
        }

        public bool LocalImageExists(string tag, bool isDryRun) => DockerHelper.LocalImageExists(tag, isDryRun);

        public long GetImageSize(string image, bool isDryRun) => DockerHelper.GetImageSize(image, isDryRun);

        public DateTime GetCreatedDate(string image, bool isDryRun)
        {
            if (isDryRun)
            {
                return default;
            }

            return DateTime.Parse(DockerHelper.GetCreatedDate(image, isDryRun));
        }
    }
}
