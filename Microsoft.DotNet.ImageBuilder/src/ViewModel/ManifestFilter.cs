// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestFilter
    {
        public Architecture DockerArchitecture { get; set; } = DockerHelper.Architecture;
        public OS IncludeOsType { get; set; } = DockerHelper.GetOS();
        public string IncludeRepo { get; set; }
        public string IncludeOsVersion { get; set; }
        public IEnumerable<string> IncludePaths { get; set; }

        public ManifestFilter()
        {
        }

        public IEnumerable<Platform> GetActivePlatforms(Image image)
        {
            return GetPlatforms(image)
                .Where(platform => platform.OS == IncludeOsType && platform.Architecture == DockerArchitecture);
        }

        private string GetFilterRegexPattern(params string[] patterns)
        {
            string processedPatterns = patterns
                .Select(pattern => Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", "."))
                .Aggregate((working, next) => $"{working}|{next}");
            return $"^({processedPatterns})$";
        }

        public IEnumerable<Platform> GetPlatforms(Image image)
        {
            IEnumerable<Platform> platforms = image.Platforms;

            if (IncludePaths?.Any() ?? false)
            {
                string pathsRegexPattern = GetFilterRegexPattern(IncludePaths.ToArray());
                platforms = platforms
                    .Where(platform => Regex.IsMatch(platform.Dockerfile, pathsRegexPattern, RegexOptions.IgnoreCase));
            }

            if (IncludeOsVersion != null)
            {
                string includeOsVersionPattern = GetFilterRegexPattern(IncludeOsVersion);
                platforms = platforms.Where(platform =>
                    Regex.IsMatch(platform.OsVersion ?? string.Empty, includeOsVersionPattern, RegexOptions.IgnoreCase));
            }

            return platforms;
        }

        public IEnumerable<Repo> GetRepos(Manifest manifest)
        {
            return manifest.Repos.Where(repo => string.IsNullOrWhiteSpace(IncludeRepo) || repo.Name == IncludeRepo);
        }
    }
}
