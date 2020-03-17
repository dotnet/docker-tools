// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateBuildMatrixCommand : ManifestCommand<GenerateBuildMatrixOptions>
    {
        private readonly static char[] s_pathSeparators = { '/', '\\' };

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
            IEnumerable<IEnumerable<PlatformInfo>> subgraphs = platformGrouping.GetCompleteSubgraphs(
                platform => GetPlatformDependencies(platform, platformGrouping));

            foreach (IEnumerable<PlatformInfo> subgraph in subgraphs)
            {
                string[] dockerfilePaths = GetDockerfilePaths(subgraph)
                    .Union(GetCustomLegGroupingDockerfilePaths(subgraph))
                    .ToArray();

                BuildLegInfo leg = new BuildLegInfo()
                {
                    Name = GetDockerfilePathLegName(dockerfilePaths, matrixNameParts)
                };
                matrix.Legs.Add(leg);

                AddImageBuilderPathsVariable(dockerfilePaths, leg);
                AddCommonVariables(platformGrouping, leg);
            }
        }

        private static string[] GetDockerfilePaths(IEnumerable<PlatformInfo> platforms)
        {
            return platforms
                .Select(platform => platform.Model.Dockerfile)
                .ToArray();
        }

        private string[] GetCustomLegGroupingDockerfilePaths(IEnumerable<PlatformInfo> platforms)
        {
            IEnumerable<PlatformInfo> subgraphs = platforms.GetCompleteSubgraphs(platform =>
                {
                    if (Options.CustomBuildLegGrouping != null &&
                        platform.CustomLegGroupings.TryGetValue(
                            Options.CustomBuildLegGrouping,
                            out CustomBuildLegGroupingInfo customBuildLegGroupingInfo))
                    {
                        return customBuildLegGroupingInfo.DependencyImages
                            .Select(image => Manifest.GetPlatformByTag(image));
                    }

                    return Enumerable.Empty<PlatformInfo>();
                })
                .SelectMany(image => image);
            return GetDockerfilePaths(subgraphs);
        }

        private static void AddImageBuilderPathsVariable(string[] dockerfilePaths, BuildLegInfo leg)
        {
            string pathArgs = dockerfilePaths
                .Select(path => $"{ManifestFilterOptions.FormattedPathOption} {path}")
                .Aggregate((working, next) => $"{working} {next}");
            leg.Variables.Add(("imageBuilderPaths", pathArgs));
        }

        private static void AddCommonVariables(IGrouping<PlatformId, PlatformInfo> platformGrouping, BuildLegInfo leg)
        {
            string fullyQualifiedLegName =
                (platformGrouping.Key.OsVersion ?? platformGrouping.Key.OS.GetDockerName()) +
                platformGrouping.Key.Architecture.GetDisplayName(platformGrouping.Key.Variant) +
                leg.Name;

            leg.Variables.Add(("legName", fullyQualifiedLegName));
            leg.Variables.Add(("osType", platformGrouping.Key.OS.GetDockerName()));
            leg.Variables.Add(("architecture", platformGrouping.Key.Architecture.GetDockerName()));
            leg.Variables.Add(("osVersion", platformGrouping.Key.OsVersion ?? "*"));
        }

        private string GetDotNetVersion(ImageInfo image)
        {
            Version version = Version.Parse(image.Model.ProductVersion);
            return version.ToString(2); // Return major.minor
        }

        private void AddVersionedOsLegs(BuildMatrixInfo matrix, IGrouping<PlatformId, PlatformInfo> platformGrouping)
        {
            var versionGroups = platformGrouping
                .GroupBy(platform => new
                {
                    // Assumption:  Dockerfile path format <ProductVersion>/<ImageVariant>/<OsVariant>/...
                    DotNetVersion = GetDotNetVersion(Manifest.GetImageByPlatform(platform)),
                    OsVariant = platform.Model.OsVersion
                });
            foreach (var versionGrouping in versionGroups)
            {
                IEnumerable<PlatformInfo> subgraphs = versionGrouping
                    .GetCompleteSubgraphs(platform => GetPlatformDependencies(platform, platformGrouping))
                    .SelectMany(subgraph => subgraph);

                BuildLegInfo leg = new BuildLegInfo() { Name = $"{versionGrouping.Key.DotNetVersion}-{versionGrouping.Key.OsVariant}" };
                matrix.Legs.Add(leg);

                AddCommonVariables(platformGrouping, leg);
                leg.Variables.Add(("dotnetVersion", versionGrouping.Key.DotNetVersion));
                leg.Variables.Add(("osVariant", versionGrouping.Key.OsVariant));

                IEnumerable<string> dockerfilePaths = GetDockerfilePaths(subgraphs)
                    .Union(GetCustomLegGroupingDockerfilePaths(subgraphs));

                AddImageBuilderPathsVariable(dockerfilePaths.ToArray(), leg);
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
                    OsVersion = platform.Model.OS == OS.Linux ? null : platform.Model.OsVersion,
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
                return platformId.OsVersion
                    .Replace("nanoserver", "windows")
                    .Replace("windowsservercore", "windows")
                    .Replace("ltsc2019", "1809");
            }

            return platformId.OS.GetDockerName();
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
