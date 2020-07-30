// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateBuildMatrixCommand : ManifestCommand<GenerateBuildMatrixOptions>
    {
        private readonly static char[] s_pathSeparators = { '/', '\\' };

        private const string VersionRegGroupName = "Version";
        private static readonly Regex s_versionRegex = new Regex(@$"^(?<{VersionRegGroupName}>(\d|\.)+).*$");

        public GenerateBuildMatrixCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING BUILD MATRIX");

            IEnumerable<BuildMatrixInfo> matrices = GenerateMatrixInfo();
            LogDiagnostics(matrices);
            EmitVstsVariables(matrices);

            return Task.CompletedTask;
        }

        private void AddDockerfilePathLegs(
            BuildMatrixInfo matrix, IEnumerable<string> matrixNameParts, IGrouping<PlatformId, PlatformInfo> platformGrouping)
        {
            // Pass 1: Find direct dependencies from the Dockerfile's FROM statement
            IEnumerable<IEnumerable<PlatformInfo>> subgraphs = platformGrouping.GetCompleteSubgraphs(
                platform => GetPlatformDependencies(platform, platformGrouping));

            // Pass 2: Find dependencies amongst the subgraphs that result from custom leg groups
            // to produce a new set of subgraphs.
            subgraphs = subgraphs.GetCompleteSubgraphs(subgraph => GetCustomLegGroupingDependencies(subgraph, subgraphs))
                .Select(set => set.SelectMany(subgraph => subgraph))
                .ToArray();

            foreach (IEnumerable<PlatformInfo> subgraph in subgraphs)
            {
                string[] dockerfilePaths = GetDockerfilePaths(subgraph).ToArray();

                BuildLegInfo leg = new BuildLegInfo()
                {
                    Name = GetDockerfilePathLegName(dockerfilePaths, matrixNameParts)
                };
                matrix.Legs.Add(leg);

                AddImageBuilderPathsVariable(dockerfilePaths, leg);
                AddCommonVariables(platformGrouping, subgraph, leg);
            }
        }

        private IEnumerable<IEnumerable<PlatformInfo>> GetCustomLegGroupingDependencies(
            IEnumerable<PlatformInfo> subgraph, IEnumerable<IEnumerable<PlatformInfo>> subgraphs, CustomBuildLegDependencyType? dependencyType = null)
        {
            IEnumerable<IEnumerable<PlatformInfo>> dependencySubgraphs = subgraph
                .Select(platform =>
                {
                    // Find the subgraphs of the platforms that are the custom leg dependencies of this platform
                    IEnumerable<IEnumerable<PlatformInfo>> dependencySubgraphs =
                        GetCustomLegGroupPlatforms(platform, dependencyType)
                            .Select(dependency =>
                                subgraphs
                                    .Where(otherSubgraph => subgraph != otherSubgraph)
                                    .FirstOrDefault(otherSubgraph => otherSubgraph.Contains(dependency)))
                            .Distinct();

                    if (dependencySubgraphs.Count() > 1)
                    {
                        throw new NotSupportedException(
                            $"Platform '{platform.DockerfilePathRelativeToManifest}' has a custom leg group dependency on more than one subgraphs.");
                    }

                    return dependencySubgraphs.FirstOrDefault();
                })
                .Distinct()
                .Where(subgraph => subgraph != null)
                .ToArray();

            return dependencySubgraphs;
        }

        private static string[] GetDockerfilePaths(IEnumerable<PlatformInfo> platforms)
        {
            return platforms
                .Select(platform => platform.Model.Dockerfile)
                .ToArray();
        }

        private IEnumerable<PlatformInfo> GetCustomLegGroupPlatforms(PlatformInfo platform, CustomBuildLegDependencyType? dependencyType)
        {
            return Options.CustomBuildLegGroups
                .Select(groupName =>
                {
                    platform.CustomLegGroups.TryGetValue(groupName, out CustomBuildLegGroup group);
                    return group;
                })
                .Where(group => group != null)
                .SelectMany(group =>
                {
                    IEnumerable<PlatformInfo> dependencyPlatforms = group.Dependencies
                        .Where(dependency => !dependencyType.HasValue || dependency.Type == dependencyType)
                        .Select(dependency => Manifest.GetPlatformByTag(dependency.ImageTag));
                    return dependencyPlatforms
                        .Concat(dependencyPlatforms
                            .SelectMany(dependencyPlatform => GetParents(dependencyPlatform, Manifest.GetFilteredPlatforms())));
                })
                .Distinct();
        }

        private static void AddImageBuilderPathsVariable(string[] dockerfilePaths, BuildLegInfo leg)
        {
            string pathArgs = dockerfilePaths
                .Select(path => $"{ManifestFilterOptions.FormattedPathOption} {path}")
                .Aggregate((working, next) => $"{working} {next}");
            leg.Variables.Add(("imageBuilderPaths", pathArgs));
        }

        private static void AddCommonVariables(
            IGrouping<PlatformId, PlatformInfo> platformGrouping, IEnumerable<PlatformInfo> subgraph, BuildLegInfo leg)
        {
            string fullyQualifiedLegName =
                (platformGrouping.Key.OsVersion ?? platformGrouping.Key.OS.GetDockerName()) +
                platformGrouping.Key.Architecture.GetDisplayName(platformGrouping.Key.Variant) +
                leg.Name;

            leg.Variables.Add(("legName", fullyQualifiedLegName));
            leg.Variables.Add(("osType", platformGrouping.Key.OS.GetDockerName()));
            leg.Variables.Add(("architecture", platformGrouping.Key.Architecture.GetDockerName()));

            string[] osVersions = subgraph
                .Select(platform => $"{ManifestFilterOptions.FormattedOsVersionOption} {platform.Model.OsVersion}")
                .Distinct()
                .ToArray();
            leg.Variables.Add(("osVersions", String.Join(" ", osVersions)));
        }

        private string GetProductVersion(ImageInfo image)
        {
            if (image.ProductVersion is null)
            {
                return null;
            }

            Match match = s_versionRegex.Match(image.ProductVersion);
            if (match.Success)
            {
                if (Options.ProductVersionComponents <= 0)
                {
                    throw new InvalidOperationException($"The {nameof(Options.ProductVersionComponents)} option must be set to a value greater than zero.");
                }

                Version version = Version.Parse(match.Groups[VersionRegGroupName].Value);

                // We can't call ToString with a number that's greater than the number of components in the actual
                // version.  So we first need to determine how many components are in the version and then get the
                // number of components specified in the options or contained by the actual version value, whichever is smaller.
                int componentCount = Math.Min(version.ToString().Count(c => c == '.') + 1, Options.ProductVersionComponents);
                return version.ToString(componentCount);
            }

            return null;            
        }

        private static string GetNormalizedOsVersion(string osVersion) =>
            osVersion?
                .Replace("nanoserver", "windows")
                .Replace("windowsservercore", "windows")
                .Replace("ltsc2019", "1809");

        private void AddVersionedOsLegs(BuildMatrixInfo matrix, IGrouping<PlatformId, PlatformInfo> platformGrouping)
        {
            // Get the set of subgraphs grouped by their FROM dependencies as well as any integral custom leg dependencies.
            IEnumerable<IEnumerable<PlatformInfo>> subgraphs = platformGrouping
                .GetCompleteSubgraphs(platform =>
                    GetPlatformDependencies(platform, platformGrouping)
                        .Union(GetCustomLegGroupPlatforms(platform, CustomBuildLegDependencyType.Integral)));

            // Append any supplemental custom leg dependencies to each subgraph
            subgraphs = subgraphs
                .Select(subgraph => subgraph.Union(subgraph.SelectMany(platform => GetCustomLegGroupPlatforms(platform, CustomBuildLegDependencyType.Supplemental))));

            // Append the parent graph of each platform to each respective subgraph
            subgraphs = subgraphs.GetCompleteSubgraphs(
                subgraph => subgraph.Select(platform => GetParents(platform, platformGrouping)))
                .Select(set => set
                    .SelectMany(subgraph => subgraph)
                    .Distinct())
                .ToArray();

            foreach (IEnumerable<PlatformInfo> subgraph in subgraphs)
            {
                string osVariant = subgraph.First().BaseOsVersion;
                string productVersion = GetProductVersion(Manifest.GetImageByPlatform(subgraph.First()));
                BuildLegInfo leg = new BuildLegInfo() { Name = $"{productVersion}-{osVariant}" };
                matrix.Legs.Add(leg);

                AddCommonVariables(platformGrouping, subgraph, leg);
                leg.Variables.Add(("productVersion", productVersion));
                leg.Variables.Add(("osVariant", osVariant));
                AddImageBuilderPathsVariable(GetDockerfilePaths(subgraph).ToArray(), leg);
            }
        }

        private static void EmitVstsVariables(IEnumerable<BuildMatrixInfo> matrices)
        {
            // Emit the special syntax to set a VSTS build definition matrix variable
            // ##vso[task.setvariable variable=x;isoutput=true]{ \"a\": { \"v1\": \"1\" }, \"b\": { \"v1\": \"2\" } }
            foreach (BuildMatrixInfo matrix in matrices)
            {
                string legs = matrix.OrderedLegs
                    .Select(leg =>
                    {
                        string variables = leg.Variables
                            .Select(var => $" \"{var.Name}\": \"{var.Value}\"")
                            .Aggregate((working, next) => $"{working},{next}");
                        return $" \"{leg.Name}\": {{{variables} }}";
                    })
                    .Aggregate((working, next) => $"{working},{next}");
                Logger.WriteMessage(PipelineHelper.FormatOutputVariable(matrix.Name, $"{{{legs}}}"));
            }
        }

        /// <summary>
        /// Builds the leg name from the specified Dockerfile path. Any parts of the Dockerfile path that
        /// are in common with the containing matrix name are trimmed. The resulting leg name uses '-' characters as word
        /// separators.
        /// </summary>
        private static string GetDockerfilePathLegName(IEnumerable<string> dockerfilePath, IEnumerable<string> matrixNameParts)
        {
            string legName = dockerfilePath.First().Split(s_pathSeparators)
                .Where(subPart => 
                    !matrixNameParts.Any(matrixPart => matrixPart.StartsWith(subPart, StringComparison.OrdinalIgnoreCase)))
                .Aggregate((working, next) => $"{working}-{next}");

            if (dockerfilePath.Count() > 1)
            {
                legName += "-graph";
            }

            return legName;
        }

        /// <summary>
        /// Formats a matrix name by joining the specified parts. The resulting matrix name is camelCased.
        /// Any '-' occurrences within the specified parts will be treated as word boundaries.
        /// </summary>
        private static string FormatMatrixName(IEnumerable<string> parts)
        {
            string[] allParts = parts.SelectMany(part => part.Split('-')).ToArray();
            return allParts.First() + string.Join(string.Empty, allParts.Skip(1).Select(part => part.FirstCharToUpper()));
        }

        public IEnumerable<BuildMatrixInfo> GenerateMatrixInfo()
        {
            List<BuildMatrixInfo> matrices = new List<BuildMatrixInfo>();

            // The sort order used here is arbitrary and simply helps the readability of the output.
            var platformGroups = Manifest.GetFilteredPlatforms()
                .GroupBy(platform => new PlatformId()
                {
                    OS = platform.Model.OS,
                    OsVersion = platform.Model.OS == OS.Linux ? null : GetNormalizedOsVersion(platform.BaseOsVersion),
                    Architecture = platform.Model.Architecture,
                    Variant = platform.Model.Variant
                })
                .OrderBy(platformGroup => platformGroup.Key.OS)
                .ThenByDescending(platformGroup => platformGroup.Key.OsVersion)
                .ThenBy(platformGroup => platformGroup.Key.Architecture)
                .ThenByDescending(platformGroup => platformGroup.Key.Variant);

            foreach (var platformGrouping in platformGroups)
            {
                string[] matrixNameParts =
                {
                    GetOsMatrixNamePart(platformGrouping.Key),
                    platformGrouping.Key.Architecture.GetDisplayName(platformGrouping.Key.Variant)
                };
                BuildMatrixInfo matrix = new BuildMatrixInfo() { Name = FormatMatrixName(matrixNameParts) };
                matrices.Add(matrix);

                if (Options.MatrixType == MatrixType.PlatformDependencyGraph)
                {
                    AddDockerfilePathLegs(matrix, matrixNameParts, platformGrouping);
                }
                else if (Options.MatrixType == MatrixType.PlatformVersionedOs)
                {
                    AddVersionedOsLegs(matrix, platformGrouping);
                }
            }

            return matrices;
        }

        private static string GetOsMatrixNamePart(PlatformId platformId)
        {
            if (platformId.OsVersion != null)
            {
                return platformId.OsVersion;
            }

            return platformId.OS.GetDockerName();
        }

        private IEnumerable<PlatformInfo> GetParents(PlatformInfo platform, IEnumerable<PlatformInfo> availablePlatforms)
        {
            List<PlatformInfo> parents = new List<PlatformInfo>();
            foreach (PlatformInfo parent in GetPlatformDependencies(platform, availablePlatforms))
            {
                parents.Add(parent);
                parents.AddRange(GetParents(parent, availablePlatforms));
            }

            return parents;
        }

        private IEnumerable<PlatformInfo> GetPlatformDependencies(PlatformInfo platform, IEnumerable<PlatformInfo> availablePlatforms) =>
            platform.InternalFromImages
                .Select(fromImage => Manifest.GetPlatformByTag(fromImage))
                .Intersect(availablePlatforms);

        private static void LogDiagnostics(IEnumerable<BuildMatrixInfo> matrices)
        {
            // Write out the matrices in a human friendly format
            foreach (BuildMatrixInfo matrix in matrices)
            {
                Logger.WriteMessage($"  {matrix.Name}:");
                foreach (BuildLegInfo leg in matrix.OrderedLegs)
                {
                    Logger.WriteMessage($"    {leg.Name}:");
                    foreach ((string Name, string Value) in leg.Variables)
                    {
                        Logger.WriteMessage($"      {Name}: {Value}");
                    }
                }
            }
        }

        private class PlatformId : IEquatable<PlatformId>
        {
            public Architecture Architecture { get; set; }
            public OS OS { get; set; }
            public string OsVersion { get; set; }
            public string Variant { get; set; }

            public bool Equals(PlatformId other)
            {
                return Architecture == other.Architecture
                    && OS == other.OS
                    && OsVersion == other.OsVersion
                    && Variant == other.Variant;
            }

            public override int GetHashCode()
            {
                return $"{Architecture}-{OS}-{OsVersion}-{Variant}".GetHashCode();
            }
        }
    }
}
