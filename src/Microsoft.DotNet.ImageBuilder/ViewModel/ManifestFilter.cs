// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestFilter
    {
        public Architecture DockerArchitecture { get; set; } = DockerHelper.Architecture;
        public string DockerOS { get; } = DockerHelper.GetOS();
        public string IncludeRepo { get; set; }
        public string IncludeOsVersion { get; set; }
        public string IncludePath { get; set; }

        public ManifestFilter()
        {
        }

        public IEnumerable<Platform> GetActivePlatforms(Image image)
        {
            return GetPlatforms(image)
                .Where(platform => string.Equals(platform.OS, DockerOS, StringComparison.OrdinalIgnoreCase)
                    && platform.Architecture == DockerArchitecture);
        }

        private string GetFilterRegexPattern(string pattern)
        {
            return pattern == null ? null : $"^{Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".")}$";
        }

        public IEnumerable<Platform> GetPlatforms(Image image)
        {
            string includePathPattern = GetFilterRegexPattern(IncludePath);
            string includeOsVersionPattern = GetFilterRegexPattern(IncludeOsVersion);

            return image.Platforms
                .Where(platform => IncludePath == null
                    || Regex.IsMatch(platform.Dockerfile, includePathPattern, RegexOptions.IgnoreCase))
                .Where(platform => IncludeOsVersion == null
                    || (platform.OsVersion != null
                        && Regex.IsMatch(platform.OsVersion, includeOsVersionPattern, RegexOptions.IgnoreCase)));
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
