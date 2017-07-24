// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestFilter
    {
        public Architecture DockerArchitecture { get; set; }
        public string DockerOS { get; } = DockerHelper.GetOS();
        public string IncludeRepo { get; set; }
        public string IncludePath { get; set; }

        public ManifestFilter()
        {
        }

        public Platform GetPlatform(Image image)
        {
            return image.Platforms
                .Where(platform => platform.OS == DockerOS && platform.Architecture == DockerArchitecture)
                .Where(platform => string.IsNullOrWhiteSpace(IncludePath) || platform.Dockerfile.StartsWith(IncludePath))
                .SingleOrDefault();
        }

        public IEnumerable<Repo> GetRepos(Manifest manifest)
        {
            return manifest.Repos.Where(repo => string.IsNullOrWhiteSpace(IncludeRepo) || repo.Name == IncludeRepo);
        }

        public IEnumerable<string> GetTestCommands(Manifest manifest)
        {
            string[] commands = null;
            manifest.TestCommands?.TryGetValue(DockerOS, out commands);
            return commands ?? Enumerable.Empty<string>();
        }
    }
}
