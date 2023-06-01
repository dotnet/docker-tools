// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateBuildMatrixCommand : ManifestCommand<GenerateBuildMatrixOptions, GenerateBuildMatrixOptionsBuilder>
    {
        private const string VersionRegGroupName = "Version";

        private readonly Lazy<ImageArtifactDetails?> _imageArtifactDetails;
        private static readonly char[] s_pathSeparators = { '/', '\\' };
        private static readonly Regex s_versionRegex = new(@$"^(?<{VersionRegGroupName}>(\d|\.)+).*$");

        public GenerateBuildMatrixCommand() : base()
        {
            _imageArtifactDetails = new Lazy<ImageArtifactDetails?>(() =>
            {
                if (Options.ImageInfoPath != null)
                {
                    return ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);
                }

                return null;
            });
        }

        protected override string Description => "Generate the Azure DevOps build matrix for building the images";

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING BUILD MATRIX");

            IEnumerable<BuildMatrixInfo> matrices = GenerateMatrixInfo();
            LogDiagnostics(matrices);
            EmitVstsVariables(matrices);

            return Task.CompletedTask;
        }

        private static IEnumerable<IEnumerable<PlatformInfo>> ConsolidateSubgraphs(
            IEnumerable<IEnumerable<PlatformInfo>> subgraphs, Func<PlatformInfo, string> getKey)
        {
            List<List<PlatformInfo>> subGraphsList = subgraphs.Select(subgraph => subgraph.ToList()).ToList();
            Dictionary<string, List<PlatformInfo>> subgraphsByRootDockerfilePath = new();
            HashSet<List<PlatformInfo>> subgraphsToDelete = new();

            foreach (List<PlatformInfo> subgraph in subGraphsList)
            {
                foreach (PlatformInfo platform in subgraph)
                {
                    string key = getKey(platform);

                    if (subgraphsByRootDockerfilePath.TryGetValue(key, out List<PlatformInfo>? commonSubgraph))
                    {
                        // In some cases it is possible to have distinct PlatformInfo instances with the same Dockerfile path.
                        // This happens in the scenario where a Dockerfile path is duplicated/redefined in a separate image such
                        // as the case for Debian images that are contained both in the 6.0 multi-arch tag as well as
                        // the 6.0-bullseye-slim multi-arch tag. To account for that scenario we need to look up by Dockerfile path
                        // but also ensure that we're not getting the same subgraph that we're currently processing. That prevents us
                        // from getting the duplicated platform that exists within the same subgraph.
                        if (commonSubgraph != subgraph)
                        {
                            commonSubgraph.AddRange(subgraph);
                            subgraphsToDelete.Add(subgraph);
                        }
                    }
                    else
                    {
                        subgraphsByRootDockerfilePath.Add(key, subgraph);
                    }
                }
            }

            foreach (List<PlatformInfo> subGraphToDelete in subgraphsToDelete)
            {
                subGraphsList.Remove(subGraphToDelete);
            }

            return subGraphsList;
        }

        private void AddDockerfilePathLegs(
            BuildMatrixInfo matrix, IEnumerable<string> matrixNameParts, IGrouping<PlatformId, PlatformInfo> platformGrouping)
        {
            // Pass 1: Find direct dependencies from the Dockerfile's FROM statement
            IEnumerable<IEnumerable<PlatformInfo>> subgraphs = platformGrouping.GetCompleteSubgraphs(
                platform => Manifest.GetParents(platform, platformGrouping));

            // Pass 2: Combine subgraphs that have a common Dockerfile path for the root image
            subgraphs = ConsolidateSubgraphs(subgraphs, platform => platform.DockerfilePath);

            // Pass 3: Find dependencies amongst the subgraphs that result from custom leg groups
            // to produce a new set of subgraphs.
            subgraphs = subgraphs.GetCompleteSubgraphs(subgraph => GetCustomLegGroupingDependencies(subgraph, subgraphs))
                .Select(set => set.SelectMany(subgraph => subgraph))
                .ToArray();

            Dictionary<string, BuildLegInfo> buildLegsByDockerfilePath = new();
            foreach (IEnumerable<PlatformInfo> subgraph in subgraphs)
            {
                string[] dockerfilePaths = GetDockerfilePaths(subgraph).ToArray();
                BuildLegInfo leg = new()
                {
                    Name = GetDockerfilePathLegName(dockerfilePaths, matrixNameParts)
                };

                // Validate that we don't end up with multiple legs building the same Dockerfile. This would lead to a conflict in
                // image publishing.
                foreach (string dockerfilePath in dockerfilePaths)
                {
                    if (buildLegsByDockerfilePath.TryGetValue(dockerfilePath, out BuildLegInfo? legWithDuplicateDockerfile))
                    {
                        throw new InvalidOperationException($"Dockerfile '{dockerfilePath}' in leg '{leg.Name}' is already included in leg '{legWithDuplicateDockerfile.Name}'. A Dockerfile can only be built in a single leg.");
                    }

                    buildLegsByDockerfilePath.Add(dockerfilePath, leg);
                }

                matrix.Legs.Add(leg);

                AddImageBuilderPathsVariable(dockerfilePaths, leg);
                AddCommonVariables(platformGrouping, subgraph, leg);
            }
        }

        private IEnumerable<IEnumerable<PlatformInfo>> GetCustomLegGroupingDependencies(
            IEnumerable<PlatformInfo> subgraph, IEnumerable<IEnumerable<PlatformInfo>> subgraphs)
        {
            IEnumerable<IEnumerable<PlatformInfo>> dependencySubgraphs = subgraph
                .Select(platform =>
                {
                    // Find the subgraphs of the platforms that are the custom leg dependencies of this platform
                    IEnumerable<IEnumerable<PlatformInfo>> dependencySubgraphs =
                        GetCustomLegGroupPlatforms(platform)
                            .Select(dependency =>
                                subgraphs
                                    .Where(otherSubgraph => subgraph != otherSubgraph)
                                    .FirstOrDefault(otherSubgraph => otherSubgraph.Contains(dependency)))
                            .Where(subgraph => subgraph is not null)
                            .Cast<IEnumerable<PlatformInfo>>()
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
                .Cast<IEnumerable<PlatformInfo>>()
                .ToArray();

            return dependencySubgraphs;
        }

        private static string[] GetDockerfilePaths(IEnumerable<PlatformInfo> platforms)
        {
            return platforms
                .Select(platform => platform.Model.Dockerfile)
                .Distinct()
                .ToArray();
        }

        private IEnumerable<PlatformInfo> GetCustomLegGroupPlatforms(PlatformInfo platform, CustomBuildLegDependencyType? dependencyType = null)
        {
            return Options.CustomBuildLegGroups
                .Select(groupName =>
                {
                    platform.CustomLegGroups.TryGetValue(groupName, out CustomBuildLegGroup? group);
                    return group;
                })
                .Where(group => group != null && (!dependencyType.HasValue || group?.Type == dependencyType))
                .Cast<CustomBuildLegGroup>()
                .SelectMany(group =>
                {
                    IEnumerable<PlatformInfo> dependencyPlatforms = group.Dependencies
                        .Select(dependency => Manifest.GetPlatformByTag(dependency));
                    return dependencyPlatforms
                        .Concat(dependencyPlatforms
                            .SelectMany(dependencyPlatform => Manifest.GetAncestors(dependencyPlatform, Manifest.GetFilteredPlatforms())));
                })
                .Distinct();
        }

        private static void AddImageBuilderPathsVariable(string[] dockerfilePaths, BuildLegInfo leg)
        {
            string pathArgs = dockerfilePaths
                .Distinct()
                .Select(path => $"{CliHelper.FormatAlias(ManifestFilterOptionsBuilder.PathOptionName)} {path}")
                .Aggregate((working, next) => $"{working} {next}");
            leg.Variables.Add(("imageBuilderPaths", pathArgs));
        }

        private static void AddCommonVariables(
            IGrouping<PlatformId, PlatformInfo> platformGrouping, IEnumerable<PlatformInfo> subgraph, BuildLegInfo leg)
        {
            string fullyQualifiedLegName =
                (platformGrouping.Key.OsVersion ?? platformGrouping.Key.OS.GetDockerName()) +
                platformGrouping.Key.Architecture.GetDisplayName() +
                leg.Name;

            leg.Variables.Add(("legName", fullyQualifiedLegName));
            leg.Variables.Add(("osType", platformGrouping.Key.OS.GetDockerName()));
            leg.Variables.Add(("architecture", platformGrouping.Key.Architecture.GetDockerName()));

            string[] osVersions = subgraph
                .Select(platform => $"{CliHelper.FormatAlias(ManifestFilterOptionsBuilder.OsVersionOptionName)} {platform.Model.OsVersion}")
                .Distinct()
                .ToArray();
            leg.Variables.Add(("osVersions", string.Join(" ", osVersions)));
        }

        private string? GetProductVersion(ImageInfo image)
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

        private static string? GetNormalizedOsVersion(string? osVersion) =>
            osVersion?
                .Replace("nanoserver", "windows")
                .Replace("windowsservercore", "windows")
                .Replace("ltsc2019", "1809");

        private void AddVersionedOsLegs(BuildMatrixInfo matrix,
            IGrouping<PlatformId, PlatformInfo> platformGrouping)
        {
            IEnumerable<PlatformInfo> allPlatforms = Manifest.GetAllPlatforms();

            // Pass 1: Get the set of subgraphs of all platforms grouped by their FROM dependencies as well as any integral custom leg dependencies.
            IEnumerable<IEnumerable<PlatformInfo>> subgraphs = platformGrouping
                .GetCompleteSubgraphs(platform =>
                    Manifest.GetParents(platform, allPlatforms)
                        .Union(GetCustomLegGroupPlatforms(platform, CustomBuildLegDependencyType.Integral)));

            // Pass 2: Filter subgraphs to only images that are in the current platform group
            subgraphs = subgraphs
                .Select(subgraph => subgraph
                    .Where(platform => platformGrouping.Contains(platform)));

            // Pass 3: Combine subgraphs that have matching roots. This combines any duplicated platforms into a single subgraph.
            subgraphs = ConsolidateSubgraphs(subgraphs,
                platform => platform.GetUniqueKey(Manifest.GetImageByPlatform(platform)));

            // Pass 4: Append any supplemental custom leg dependencies to each subgraph
            subgraphs = subgraphs
                .Select(subgraph => subgraph.Union(subgraph.SelectMany(platform => GetCustomLegGroupPlatforms(platform, CustomBuildLegDependencyType.Supplemental))));

            // Pass 5: Append the parent graph of each platform to each respective subgraph
            subgraphs = subgraphs.GetCompleteSubgraphs(
                subgraph => subgraph.Select(platform => Manifest.GetAncestors(platform, platformGrouping)))
                .Select(set => set
                    .SelectMany(subgraph => subgraph)
                    .Distinct())
                .ToArray();

            List<List<PlatformInfo>> consolidatedSubGraphs = ConsolidateSubGraphs(subgraphs);

            foreach (IEnumerable<PlatformInfo> subgraph in consolidatedSubGraphs)
            {
                PlatformInfo platform = subgraph.First();
                ImageInfo image = Manifest.GetImageByPlatform(platform);
                string osVariant = platform.BaseOsVersion;
                string? productVersion = GetProductVersion(image);
                BuildLegInfo leg = new()
                {
                    Name = $"{(productVersion is not null ? productVersion + "-" : string.Empty)}{osVariant}-{Manifest.GetRepoByImage(image).Id}"
                        .Replace("/", "-")
                };
                matrix.Legs.Add(leg);

                AddCommonVariables(platformGrouping, subgraph, leg);
                leg.Variables.Add(("productVersion", productVersion));
                leg.Variables.Add(("osVariant", osVariant));
                AddImageBuilderPathsVariable(GetDockerfilePaths(subgraph).ToArray(), leg);
            }
        }

        private List<List<PlatformInfo>> ConsolidateSubGraphs(IEnumerable<IEnumerable<PlatformInfo>> subgraphs)
        {
            // Combine platforms which share Dockerfile paths and product version.
            List<List<PlatformInfo>> consolidatedSubGraphs = new();
            foreach (IEnumerable<PlatformInfo> graph in subgraphs)
            {
                // Look through the list of consolidated graphs we've collected so far. Find the one, if any, that has a platform
                // which matches.
                List<PlatformInfo>? matchingPlatformGraph = consolidatedSubGraphs
                    .FirstOrDefault(consolidatedGraph =>
                        consolidatedGraph.Any(consolidatedPlatform =>
                            graph.Any(platform =>
                                platform.DockerfilePathRelativeToManifest == consolidatedPlatform.DockerfilePathRelativeToManifest &&
                                GetProductVersion(Manifest.GetImageByPlatform(platform)) ==
                                    GetProductVersion(Manifest.GetImageByPlatform(consolidatedPlatform)))));

                if (matchingPlatformGraph is not null)
                {
                    matchingPlatformGraph.AddRange(graph);
                }
                else
                {
                    consolidatedSubGraphs.Add(graph.ToList());
                }
            }

            return consolidatedSubGraphs;
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

        private IEnumerable<PlatformInfo> GetPlatforms()
        {
            if (_imageArtifactDetails.Value is null)
            {
                return Manifest.GetFilteredPlatforms();
            }

            IEnumerable<PlatformInfo>? platforms = _imageArtifactDetails.Value.Repos?
                .SelectMany(repo => repo.Images)
                .SelectMany(image => image.Platforms)
                .Where(platform => !platform.IsUnchanged)
                .Select(platform => platform.PlatformInfo)
                .Where(platform => platform != null)
                .Cast<PlatformInfo>();

            return platforms ?? Enumerable.Empty<PlatformInfo>();
        }

        public IEnumerable<BuildMatrixInfo> GenerateMatrixInfo()
        {
            List<BuildMatrixInfo> matrices = new();

            // The sort order used here is arbitrary and simply helps the readability of the output.
            IOrderedEnumerable<IGrouping<PlatformId, PlatformInfo>> platformGroups = GetPlatforms()
                .GroupBy(platform => CreatePlatformId(platform))
                .OrderBy(platformGroup => platformGroup.Key.OS)
                .ThenByDescending(platformGroup => platformGroup.Key.OsVersion)
                .ThenBy(platformGroup => platformGroup.Key.Architecture);

            foreach (IGrouping<PlatformId, PlatformInfo> platformGrouping in platformGroups)
            {
                string[] matrixNameParts =
                {
                    GetOsMatrixNamePart(platformGrouping.Key),
                    platformGrouping.Key.Architecture.GetDisplayName()
                };
                BuildMatrixInfo matrix = new() { Name = FormatMatrixName(matrixNameParts) };
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

            // Guard against any duplicate leg names: https://github.com/dotnet/docker-tools/issues/891
            foreach (BuildMatrixInfo matrix in matrices)
            {
                List<string> duplicateLegNames = matrix.Legs
                    .Select(leg => leg.Name)
                    .GroupBy(name => name)
                    .Where(grouping => grouping.Count() > 1)
                    .Select(grouping => grouping.Key)
                    .ToList();

                if (duplicateLegNames.Any())
                {
                    throw new InvalidOperationException(
                        $"Duplicate leg name(s) found in matrix '{matrix.Name}': {string.Join(", ", duplicateLegNames)}");
                }
            }

            return matrices;
        }

        private PlatformId CreatePlatformId(PlatformInfo platform)
        {
            string? osVersion;

            if (Options.DistinctMatrixOsVersions.Contains(platform.BaseOsVersion))
            {
                osVersion = platform.BaseOsVersion;
            }
            else if (platform.Model.OS != OS.Linux)
            {
                osVersion = GetNormalizedOsVersion(platform.BaseOsVersion);
            }
            else
            {
                osVersion = null;
            }

            return new PlatformId
            {
                OS = platform.Model.OS,
                OsVersion = osVersion,
                Architecture = platform.Model.Architecture
            };
        }

        private static string GetOsMatrixNamePart(PlatformId platformId)
        {
            if (platformId.OsVersion != null)
            {
                return platformId.OsVersion;
            }

            return platformId.OS.GetDockerName();
        }

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
            public string? OsVersion { get; set; }

            public bool Equals(PlatformId? other)
            {
                if (other is null)
                {
                    return false;
                }

                return Architecture == other.Architecture
                    && OS == other.OS
                    && OsVersion == other.OsVersion;
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as PlatformId);
            }

            public override int GetHashCode()
            {
                return $"{Architecture}-{OS}-{OsVersion}".GetHashCode();
            }
        }
    }
}
#nullable disable
