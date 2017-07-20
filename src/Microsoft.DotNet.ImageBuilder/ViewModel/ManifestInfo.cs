// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestInfo
    {
        public IEnumerable<ImageInfo> ActiveImages { get; private set; }
        public IEnumerable<string> ActivePlatformFullyQualifiedTags { get; private set; }
        private string DockerOS { get; set; }
        public IEnumerable<ImageInfo> Images { get; private set; }
        public Manifest Model { get; private set; }
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

        public static ManifestInfo Create(
            string repoJsonPath, Architecture dockerArchitecture, string includeRepo, string includePath)
        {
            ManifestInfo manifestInfo = new ManifestInfo();
            manifestInfo.DockerOS = DockerHelper.GetOS();
            string json = File.ReadAllText(repoJsonPath);
            manifestInfo.Model = JsonConvert.DeserializeObject<Manifest>(json);
            manifestInfo.Repos = manifestInfo.Model.Repos
                .Where(repo => string.IsNullOrWhiteSpace(includeRepo) || repo.Name == includeRepo)
                .Select(repo => RepoInfo.Create(
                    repo, manifestInfo.Model, dockerArchitecture, manifestInfo.DockerOS, includePath))
                .ToArray();
            manifestInfo.Images = manifestInfo.Repos
                .SelectMany(repo => repo.Images)
                .ToArray();
            manifestInfo.ActiveImages = manifestInfo.Images
                .Where(image => image.ActivePlatform != null)
                .ToArray();
            manifestInfo.ActivePlatformFullyQualifiedTags = manifestInfo.ActiveImages
                .SelectMany(image => image.ActivePlatform.FullyQualifiedTags)
                .ToArray();

            return manifestInfo;
        }

        public bool IsExternalImage(string image)
        {
            return Repos.All(repo => repo.IsExternalImage(image));
        }
    }
}
