// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestInfo
    {
        private string DockerOS { get; set; }
        public IEnumerable<ImageInfo> Images { get; private set; }
        public Manifest Model { get; private set; }
        public IEnumerable<string> PlatformTags { get; private set; }
        public IEnumerable<RepoInfo> Repos { get; private set; }

        public IEnumerable<string> TestCommands
        {
            get
            {
                string[] commands = null;
                Model.TestCommands?.TryGetValue(DockerOS, out commands);
                return commands ?? Enumerable.Empty<string>();
            }
        }

        private ManifestInfo()
        {
        }

        public static ManifestInfo Create(string repoJsonPath)
        {
            ManifestInfo manifestInfo = new ManifestInfo();
            manifestInfo.InitializeDockerOS();
            string json = File.ReadAllText(repoJsonPath);
            manifestInfo.Model = JsonConvert.DeserializeObject<Manifest>(json);
            manifestInfo.Repos = manifestInfo.Model.Repos
                .Select(image => RepoInfo.Create(image, manifestInfo.DockerOS))
                .ToArray();
            manifestInfo.Images = manifestInfo.Repos
                .SelectMany(repo => repo.Images)
                .ToArray();
            manifestInfo.PlatformTags = manifestInfo.Images
                .Where(image => image.Platform != null)
                .SelectMany(image => image.Platform.Tags)
                .ToArray();

            return manifestInfo;
        }

        private void InitializeDockerOS()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("docker", "version -f \"{{ .Server.Os }}\"");
            startInfo.RedirectStandardOutput = true;
            Process process = ExecuteHelper.Execute(startInfo, false, $"Failed to detect Docker Mode");
            DockerOS = process.StandardOutput.ReadToEnd().Trim();
        }
    }
}
