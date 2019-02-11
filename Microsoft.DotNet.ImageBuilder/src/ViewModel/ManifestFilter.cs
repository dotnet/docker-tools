// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ImageBuilder.Model;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestFilter
    {
        public string IncludeArchitecture { get; set; }
        public string IncludeOsType { get; set; }
        public string IncludeRepo { get; set; }
        public string IncludeOsVersion { get; set; }
        public IEnumerable<string> IncludePaths { get; set; }

        public ManifestFilter()
        {
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

            if (IncludeArchitecture != null)
            {
                string archRegexPattern = GetFilterRegexPattern(IncludeArchitecture);
                platforms = platforms.Where(platform =>
                    Regex.IsMatch(platform.Architecture.ToString().ToLowerInvariant(), archRegexPattern, RegexOptions.IgnoreCase));
            }

            if (IncludeOsType != null)
            {
                string osTypeRegexPattern = GetFilterRegexPattern(IncludeOsType);
                platforms = platforms.Where(platform =>
                    Regex.IsMatch(platform.OS.ToString().ToLowerInvariant(), osTypeRegexPattern, RegexOptions.IgnoreCase));
            }

            if (IncludePaths?.Any() ?? false)
            {
                string pathsRegexPattern = GetFilterRegexPattern(IncludePaths.ToArray());
                platforms = platforms.Where(platform =>
                    Regex.IsMatch(platform.Dockerfile, pathsRegexPattern, RegexOptions.IgnoreCase));
            }

            if (IncludeOsVersion != null)
            {
                string includeOsVersionPattern = GetFilterRegexPattern(IncludeOsVersion);
                platforms = platforms.Where(platform =>
                    Regex.IsMatch(platform.OsVersion ?? string.Empty, includeOsVersionPattern, RegexOptions.IgnoreCase));
            }

            return platforms.ToArray();
        }

        public IEnumerable<Repo> GetRepos(Manifest manifest)
        {
            return manifest.Repos
                .Where(repo => string.IsNullOrWhiteSpace(IncludeRepo) || repo.Name == IncludeRepo)
                .ToArray();
        }
    }
}
