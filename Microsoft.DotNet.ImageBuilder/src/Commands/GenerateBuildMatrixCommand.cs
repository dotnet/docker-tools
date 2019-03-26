// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateBuildMatrixCommand : Command<GenerateBuildMatrixOptions>
    {
        private readonly static char[] s_pathSeparators = { '/', '\\' };

        public GenerateBuildMatrixCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING BUILD MATRIX");

            IEnumerable<MatrixInfo> matrices = GenerateMatrixInfo();
            LogDiagnostics(matrices);
            EmitVstsVariables(matrices);

            return Task.CompletedTask;
        }

        private void AddDockerfilePathLegs(
            MatrixInfo matrix, IEnumerable<string> matrixNameParts, IGrouping<PlatformId, PlatformInfo> platformGrouping)
        {
            IEnumerable<string> platformNameParts = new string[] {
                platformGrouping.Key.OsVersion ?? platformGrouping.Key.OS.GetDockerName(),
                platformGrouping.Key.Architecture.GetDockerName(),
            };

            IEnumerable<IEnumerable<PlatformInfo>> subgraphs = platformGrouping.GetCompleteSubgraphs(GetPlatformDependencies);
            foreach (IEnumerable<PlatformInfo> subgraph in subgraphs)
            {
                string[] dockerfilePaths = GetDockerfilePaths(subgraph);

                LegInfo leg = new LegInfo()
                {
                    Name = GetDockerfilePathLegName(dockerfilePaths, platformNameParts, matrixNameParts)
                };
                matrix.Legs.Add(leg);

                AddImageBuilderPathsVariable(dockerfilePaths, leg);
                AddPlatformVariables(platformGrouping, leg);
            }
        }

        private static string[] GetDockerfilePaths(IEnumerable<PlatformInfo> platforms)
        {
            return platforms
                .Select(platform => platform.Model.Dockerfile)
                .ToArray();
        }

        private static void AddImageBuilderPathsVariable(string[] dockerfilePaths, LegInfo leg)
        {
            string pathArgs = dockerfilePaths
                .Select(path => $"--path {path}")
                .Aggregate((working, next) => $"{working} {next}");
            leg.Variables.Add(("imageBuilderPaths", pathArgs));
        }

        private static void AddPlatformVariables(IGrouping<PlatformId, PlatformInfo> platformGrouping, LegInfo leg)
        {
            leg.Variables.Add(("osType", platformGrouping.Key.OS.GetDockerName()));
            leg.Variables.Add(("architecture", platformGrouping.Key.Architecture.GetDockerName()));
            leg.Variables.Add(("osVersion", platformGrouping.Key.OsVersion ?? "*"));
        }

        private static void AddVersionedOsLegs(MatrixInfo matrix, IGrouping<PlatformId, PlatformInfo> platformGrouping)
        {
            var versionGroups = platformGrouping
                .GroupBy(platform => new
                {
                    // Assumption:  Dockerfile path format <ProductVersion>/<ImageVariant>/<OsVariant>/...
                    DotNetVersion = platform.DockerfilePath.Split(s_pathSeparators)[0],
                    OsVariant = platform.DockerfilePath.Split(s_pathSeparators)[2].TrimEnd("-slim")
                });
            foreach (var versionGrouping in versionGroups)
            {
                LegInfo leg = new LegInfo() { Name = $"{versionGrouping.Key.DotNetVersion}-{versionGrouping.Key.OsVariant}" };
                matrix.Legs.Add(leg);

                AddPlatformVariables(platformGrouping, leg);
                leg.Variables.Add(("dotnetVersion", versionGrouping.Key.DotNetVersion));
                leg.Variables.Add(("osVariant", versionGrouping.Key.OsVariant));

                string[] dockerfilePaths = GetDockerfilePaths(versionGrouping);
                AddImageBuilderPathsVariable(dockerfilePaths, leg);
            }
        }

        private static void EmitVstsVariables(IEnumerable<MatrixInfo> matrices)
        {
            // Emit the special syntax to set a VSTS build definition matrix variable
            // ##vso[task.setvariable variable=x;isoutput=true]{ \"a\": { \"v1\": \"1\" }, \"b\": { \"v1\": \"2\" } }
            foreach (MatrixInfo matrix in matrices)
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
                Logger.WriteMessage($"##vso[task.setvariable variable={matrix.Name};isoutput=true]{{{legs} }}");
            }
        }

        /// <summary>
        /// Builds the leg name from the specified Dockerfile path and platform grouping. Any parts of the Dockerfile path that
        /// are in common with the containing matrix name are trimmed. The resulting leg name uses '-' characters as word
        /// separators.
        /// </summary>
        private static string GetDockerfilePathLegName(
            IEnumerable<string> dockerfilePath, IEnumerable<string> platformGroupingParts, IEnumerable<string> matrixNameParts)
        {
            string legName = dockerfilePath.First().Split(s_pathSeparators)
                .Concat(platformGroupingParts)
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
            return allParts.First() +
                string.Join(string.Empty, allParts.Skip(1).Select(part => char.ToUpper(part[0]) + part.Substring(1)));
        }

        private IEnumerable<MatrixInfo> GenerateMatrixInfo()
        {
            List<MatrixInfo> matrices = new List<MatrixInfo>();

            // The sort order used here is arbitrary and simply helps the readability of the output.
            var platformGroups = Manifest.GetFilteredPlatforms()
                .GroupBy(platform => new PlatformId()
                {
                    OS = platform.Model.OS,
                    OsVersion = platform.Model.OsVersion,
                    Architecture = platform.Model.Architecture,
                    Variant = platform.Model.Variant
                })
                .OrderBy(platformGroup => platformGroup.Key.OS)
                .ThenByDescending(platformGroup => platformGroup.Key.OsVersion)
                .ThenBy(platformGroup => platformGroup.Key.Architecture)
                .ThenByDescending(platformGroup => platformGroup.Key.Variant);

            string baseMatrixName = $"{Options.MatrixType.ToString().ToLowerInvariant()}Matrix";

            if (Options.MatrixType == MatrixType.Publish)
            {
                MatrixInfo matrix = new MatrixInfo() { Name = baseMatrixName };
                matrices.Add(matrix);
                foreach (var platformGrouping in platformGroups)
                {
                    AddDockerfilePathLegs(matrix, Enumerable.Empty<string>(), platformGrouping);
                }
            }
            else
            {
                foreach (var platformGrouping in platformGroups)
                {
                    string[] matrixNameParts =
                    {
                        baseMatrixName,
                        platformGrouping.Key.OsVersion ?? platformGrouping.Key.OS.GetDockerName(),
                        platformGrouping.Key.Architecture.GetDisplayName(platformGrouping.Key.Variant)
                    };
                    MatrixInfo matrix = new MatrixInfo() { Name = FormatMatrixName(matrixNameParts) };
                    matrices.Add(matrix);

                    if (Options.MatrixType == MatrixType.Build)
                    {
                        // If we're generating the build matrix for a PR build, we want the matrix to be structured
                        // in the same way as the test matrix where te grouping is based on the combination of .NET
                        // version and OS/arch instead of just the OS/arch.  That's because PR builds wil run tests
                        // in the same job as the images are built so the tests will need access to the full set of 
                        // images (runtime/sdk, etc.).
                        if (Options.IsPullRequestBuild)
                        {
                            AddVersionedOsLegs(matrix, platformGrouping);
                        }
                        else
                        {
                            AddDockerfilePathLegs(matrix, matrixNameParts, platformGrouping);
                        }
                    }
                    else if (Options.MatrixType == MatrixType.Test)
                    {
                        AddVersionedOsLegs(matrix, platformGrouping);
                    }
                }
            }

            return matrices;
        }

        private IEnumerable<PlatformInfo> GetPlatformDependencies(PlatformInfo platform) =>
            platform.InternalFromImages.Select(fromImage => Manifest.GetPlatformByTag(fromImage));

        private static void LogDiagnostics(IEnumerable<MatrixInfo> matrices)
        {
            // Write out the matrices in a human friendly format
            foreach (MatrixInfo matrix in matrices)
            {
                Logger.WriteMessage($"  {matrix.Name}:");
                foreach (LegInfo leg in matrix.OrderedLegs)
                {
                    Logger.WriteMessage($"    {leg.Name}:");
                    foreach ((string Name, string Value) in leg.Variables)
                    {
                        Logger.WriteMessage($"      {Name}: {Value}");
                    }
                }
            }
        }

        private class MatrixInfo
        {
            public string Name { get; set; }
            public List<LegInfo> Legs { get; } = new List<LegInfo>();

            public IEnumerable<LegInfo> OrderedLegs { get => Legs.OrderBy(leg => leg.Name); }
        }

        private class LegInfo
        {
            public string Name { get; set; }
            public List<(string Name, string Value)> Variables { get; } = new List<(string, string)>();
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
