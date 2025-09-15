// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public class DockerService : IDockerService
    {
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
            bool isRetryEnabled,
            bool isDryRun)
        {
            string tagArgs = $"-t {string.Join(" -t ", tags)}";

            IEnumerable<string> buildArgList = buildArgs
                .Select(buildArg => $" --build-arg {buildArg.Key}={buildArg.Value}");
            string buildArgsString = string.Join(string.Empty, buildArgList);

            string dockerArgs = $"build --platform {platform} {tagArgs} -f {dockerfilePath}{buildArgsString} {buildContextPath}";

            if (isRetryEnabled)
            {
                return ExecuteHelper.ExecuteWithRetry("docker", dockerArgs, isDryRun);
            }
            else
            {
                return ExecuteHelper.Execute("docker", dockerArgs, isDryRun);
            }
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
#nullable disable
