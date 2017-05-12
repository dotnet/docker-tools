// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Newtonsoft.Json;
using System;
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

        public string[] TestCommands
        {
            get
            {
                Model.TestCommands.TryGetValue(DockerOS, out string[] commands);
                return commands;
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
            manifestInfo.Images = manifestInfo.Model.Images
                .Select(image => ImageInfo.Create(image, manifestInfo.DockerOS, manifestInfo.Model))
                .ToArray();

            return manifestInfo;
        }

        public IEnumerable<string> GetPlatformTags()
        {
            return Images
                .Where(image => image.Platform != null)
                .SelectMany(image => image.Platform.Tags);
        }

        public string GetReadme()
        {
            return File.ReadAllText(Model.Readme);
        }

        private void InitializeDockerOS()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("docker", "version -f \"{{ .Server.Os }}\"");
            startInfo.RedirectStandardOutput = true;
            Process process = ExecuteHelper.Execute(startInfo, false, $"Failed to detect Docker Mode");
            DockerOS = process.StandardOutput.ReadToEnd().Trim();
        }

        public override string ToString()
        {
            string images = Images
                .Select(image => image.ToString())
                .Aggregate((working, next) => $"{working}{Environment.NewLine}----------{Environment.NewLine}{next}");

            return
$@"DockerOS:  {DockerOS}
DockerRepo:  {Model.DockerRepo}
ReadmePath:  {Model.Readme}
TestCommands:
{string.Join(Environment.NewLine, TestCommands)}
Images [
{images}
]";
        }
    }
}
