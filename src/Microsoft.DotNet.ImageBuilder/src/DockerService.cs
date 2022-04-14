// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IDockerService))]
    internal class DockerService : IDockerService
    {
        private readonly IManifestToolService _manifestToolService;

        public Architecture Architecture => DockerHelper.Architecture;

        [ImportingConstructor]
        public DockerService(IManifestToolService manifestToolService)
        {
            _manifestToolService = manifestToolService ?? throw new ArgumentNullException(nameof(manifestToolService));
        }

        public async Task<string?> GetImageDigestAsync(string image, bool isDryRun)
        {
            IEnumerable<string> digests = DockerHelper.GetImageDigests(image, isDryRun);

            // A digest will not exist for images that have been built locally or have been manually installed
            if (!digests.Any())
            {
                return null;
            }

            string digestSha = await _manifestToolService.GetManifestDigestShaAsync(ManifestMediaType.Any, image, isDryRun);

            if (digestSha is null)
            {
                return null;
            }

            string digest = DockerHelper.GetDigestString(DockerHelper.GetRepo(image), digestSha);

            if (!digests.Contains(digest))
            {
                throw new InvalidOperationException(
                    $"Found published digest '{digestSha}' for tag '{image}' but could not find a matching digest value from " +
                    $"the set of locally pulled digests for this tag: { string.Join(", ", digests) }. This most likely means that " +
                    "this tag has been updated since it was last pulled.");
            }

            return digest;
        }

        public Task<IEnumerable<string>> GetImageManifestLayersAsync(string image, bool isDryRun) =>
            _manifestToolService.GetImageLayersAsync(image, isDryRun);

        public void PullImage(string image, string? platform, bool isDryRun) => DockerHelper.PullImage(image, platform, isDryRun);

        public void PushImage(string tag, bool isDryRun) => ExecuteHelper.ExecuteWithRetry("docker", $"push {tag}", isDryRun);

        public void CreateTag(string image, string tag, bool isDryRun) => DockerHelper.CreateTag(image, tag, isDryRun);

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
