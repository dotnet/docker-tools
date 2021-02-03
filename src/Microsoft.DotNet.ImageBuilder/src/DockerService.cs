// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IDockerService))]
    internal class DockerService : IDockerService
    {
        private readonly IManifestToolService _manifestToolService;
        private bool _loggedIntoDockerHub;

        public Architecture Architecture => DockerHelper.Architecture;

        public bool IsAnonymousAccessAllowed { get; set; }

        [ImportingConstructor]
        public DockerService(IManifestToolService manifestToolService)
        {
            _manifestToolService = manifestToolService ?? throw new ArgumentNullException(nameof(manifestToolService));
        }

        public string? GetImageDigest(string image, bool isDryRun)
        {
            CheckDockerHubLogin(image);

            IEnumerable<string> digests = DockerHelper.GetImageDigests(image, isDryRun);

            // A digest will not exist for images that have been built locally or have been manually installed
            if (!digests.Any())
            {
                return null;
            }

            string digestSha = _manifestToolService.GetManifestDigestSha(ManifestMediaType.Any, image, isDryRun);

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

        public IEnumerable<string> GetImageLayers(string image, bool isDryRun) => DockerHelper.GetImageLayers(image, isDryRun);

        public void PullImage(string image, bool isDryRun)
        {
            CheckDockerHubLogin(image);
            ExecuteHelper.ExecuteWithRetry("docker", $"pull {image}", isDryRun);
        }

        public void PushImage(string tag, bool isDryRun) => ExecuteHelper.ExecuteWithRetry("docker", $"push {tag}", isDryRun);

        public void CreateTag(string image, string tag, bool isDryRun) => DockerHelper.CreateTag(image, tag, isDryRun);

        public string BuildImage(
            string dockerfilePath,
            string buildContextPath,
            IEnumerable<string> tags,
            IDictionary<string, string> buildArgs,
            bool isRetryEnabled,
            bool isDryRun)
        {
            string tagArgs = $"-t {string.Join(" -t ", tags)}";

            IEnumerable<string> buildArgList = buildArgs
                .Select(buildArg => $" --build-arg {buildArg.Key}={buildArg.Value}");
            string buildArgsString = string.Join(string.Empty, buildArgList);

            string dockerArgs = $"build {tagArgs} -f {dockerfilePath}{buildArgsString} {buildContextPath}";

            if (isRetryEnabled)
            {
                return ExecuteHelper.ExecuteWithRetry("docker", dockerArgs, isDryRun);
            }
            else
            {
                return ExecuteHelper.Execute("docker", dockerArgs, isDryRun);
            }
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

        public void Login(string username, string password, string? server, bool isDryRun)
        {
            Version clientVersion = DockerHelper.GetClientVersion();
            if (clientVersion >= new Version(17, 7))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(
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

            if (server is null)
            {
                _loggedIntoDockerHub = true;
            }
        }

        public void Logout(string? server, bool isDryRun)
        {
            ExecuteHelper.ExecuteWithRetry("docker", $"logout {server}", isDryRun);

            if (server is null)
            {
                _loggedIntoDockerHub = false;
            }
        }

        private void CheckDockerHubLogin(string imageName)
        {
            if (DockerHelper.GetRegistry(imageName) is null &&
                !IsAnonymousAccessAllowed &&
                !_loggedIntoDockerHub)
            {
                throw new InvalidOperationException(
                    "A Docker operation was attempted that requires being logged in with Docker Hub credentials.");
            }
        }
    }
}
#nullable disable
