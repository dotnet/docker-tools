// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestFilter
    {
        public string IncludeArchitecture { get; set; }
        public string IncludeOsType { get; set; }
        public IEnumerable<string> IncludeRepos { get; set; }
        public IEnumerable<string> IncludeOsVersions { get; set; }
        public IEnumerable<string> IncludePaths { get; set; }
        public IEnumerable<string> IncludeProductVersions { get; set; }

        public ManifestFilter()
        {
        }

        public static string GetFilterRegexPattern(params string[] patterns)
        {
            string processedPatterns = patterns
                .Select(pattern => Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", "."))
                .Aggregate((working, next) => $"{working}|{next}");
            return $"^({processedPatterns})$";
        }

        public IEnumerable<Platform> FilterPlatforms(IEnumerable<Platform> platforms, string resolvedProductVersion)
        {
            if (IncludeProductVersions?.Any() ?? false)
            {
                string includeProductVersionsPattern = GetFilterRegexPattern(IncludeProductVersions.ToArray());
                if (!Regex.IsMatch(resolvedProductVersion, includeProductVersionsPattern, RegexOptions.IgnoreCase))
                {
                    return Enumerable.Empty<Platform>();
                }
            }

            if (!string.IsNullOrEmpty(IncludeArchitecture))
            {
                string archRegexPattern = GetFilterRegexPattern(IncludeArchitecture);
                platforms = platforms.Where(platform =>
                    Regex.IsMatch(platform.Architecture.GetDockerName(), archRegexPattern, RegexOptions.IgnoreCase));
            }

            if (!string.IsNullOrEmpty(IncludeOsType))
            {
                string osTypeRegexPattern = GetFilterRegexPattern(IncludeOsType);
                platforms = platforms.Where(platform =>
                    Regex.IsMatch(platform.OS.GetDockerName(), osTypeRegexPattern, RegexOptions.IgnoreCase));
            }

            if (IncludePaths?.Any() ?? false)
            {
                string pathsRegexPattern = GetFilterRegexPattern(IncludePaths.ToArray());
                platforms = platforms.Where(platform =>
                    Regex.IsMatch(platform.Dockerfile, pathsRegexPattern, RegexOptions.IgnoreCase));
            }

            if (IncludeOsVersions?.Any() ?? false)
            {
                string includeOsVersionsPattern = GetFilterRegexPattern(IncludeOsVersions.ToArray());
                platforms = platforms.Where(platform =>
                    Regex.IsMatch(platform.OsVersion ?? string.Empty, includeOsVersionsPattern, RegexOptions.IgnoreCase));
            }

            return platforms.ToArray();
        }

        public IEnumerable<Repo> GetRepos(Manifest manifest)
        {
            if (IncludeRepos == null || !IncludeRepos.Any())
            {
                return manifest.Repos;
            }

            return manifest.Repos
                .Where(repo => IncludeRepos.Contains(repo.Name))
                .ToArray();
        }
    }
}
