// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder
{
    public class DockerService : IDockerService
    {
        private const string BuildSecretEnvironmentVariablePrefix = "IMAGEBUILDER_BUILD_SECRET_";

        public Architecture Architecture => DockerHelper.Architecture;

        public void PullImage(string image, string? platform, bool isDryRun, CancellationToken cancellationToken) => DockerHelper.PullImage(image, platform, isDryRun, cancellationToken);

        public void PushImage(string tag, bool isDryRun, CancellationToken cancellationToken) =>
            ExecuteHelper.ExecuteWithRetry("docker", $"push {tag}", isDryRun, cancellationToken);

        public void PushManifestList(string manifestListTag, bool isDryRun, CancellationToken cancellationToken) =>
            ExecuteHelper.ExecuteWithRetry("docker", $"manifest push {manifestListTag}", isDryRun, cancellationToken);

        public void CreateTag(string image, string tag, bool isDryRun, CancellationToken cancellationToken) => DockerHelper.CreateTag(image, tag, isDryRun, cancellationToken);

        public void CreateManifestList(string manifestListTag, IEnumerable<string> images, bool isDryRun, CancellationToken cancellationToken) =>
            // Use the --amend option to handle potential retries: https://github.com/dotnet/docker-tools/issues/1098
            ExecuteHelper.ExecuteWithRetry(
                "docker", $"manifest create --amend {manifestListTag} {string.Join(' ', images.ToArray())}", isDryRun, cancellationToken);

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
            bool isDryRun,
            CancellationToken cancellationToken)
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
                return ExecuteHelper.ExecuteWithRetry(processStartInfo, cancellationToken, isDryRun: isDryRun);
            }
            else
            {
                return ExecuteHelper.Execute(processStartInfo, isDryRun, cancellationToken);
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

        public (Architecture Arch, string? Variant) GetImageArch(string image, bool isDryRun, CancellationToken cancellationToken)
        {
            string archAndVariant = DockerHelper.ExecuteCommand(
                "inspect", "Failed to retrieve image architecture", cancellationToken, $"-f \"{{{{ .Architecture }}}}/{{{{ .Variant }}}}\" {image}", isDryRun);
            string[] parts = archAndVariant.Split('/', StringSplitOptions.RemoveEmptyEntries);
            Architecture arch = Enum.Parse<Architecture>(parts[0], ignoreCase: true);
            string? variant = parts.Length > 1 ? parts[1] : null;
            return (arch, variant);
        }

        public bool LocalImageExists(string tag, bool isDryRun, CancellationToken cancellationToken) => DockerHelper.LocalImageExists(tag, isDryRun, cancellationToken);

        public long GetImageSize(string image, bool isDryRun, CancellationToken cancellationToken) => DockerHelper.GetImageSize(image, isDryRun, cancellationToken);

        public DateTime GetCreatedDate(string image, bool isDryRun, CancellationToken cancellationToken)
        {
            if (isDryRun)
            {
                return default;
            }

            return DateTime.Parse(DockerHelper.GetCreatedDate(image, isDryRun, cancellationToken));
        }
    }
}
