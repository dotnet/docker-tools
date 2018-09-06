// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateBuildMatrixCommand : Command<GenerateBuildMatrixOptions>
    {
        private readonly static char[] PathSeparators = { '/', '\\' };

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

        private void AddBuildLegs(MatrixInfo matrix, string[] matrixNameParts, IGrouping<dynamic, PlatformInfo> platformGrouping)
        {
            IEnumerable<IEnumerable<PlatformInfo>> subgraphs = platformGrouping.GetCompleteSubgraphs(GetPlatformDependencies);
            foreach (IEnumerable<PlatformInfo> subgraph in subgraphs)
            {
                string[] dockerfilePaths = subgraph
                    .Select(platform => platform.Model.Dockerfile)
                    .ToArray();
                LegInfo leg = new LegInfo() { Name = FormatLegName(dockerfilePaths, matrixNameParts) };
                matrix.Legs.Add(leg);

                string pathArgs = dockerfilePaths
                    .Select(path => $"--path {path}")
                    .Aggregate((working, next) => $"{working} {next}");
                leg.Variables.Add(("imageBuilderPaths", pathArgs));
            }
        }

        private void AddTestLegs(MatrixInfo matrix, string[] matrixNameParts, IGrouping<dynamic, PlatformInfo> platformGrouping)
        {
            var versionGroups = platformGrouping
                .GroupBy(platform => new
                {
                    // Assumption:  Dockerfile path format <ProductVersion>/<ImageVariant>/<OsVariant>/...
                    DotNetVersion = platform.DockerfilePath.Split(PathSeparators)[0],
                    OsVersion = platform.DockerfilePath.Split(PathSeparators)[2].TrimEnd("-slim")
                })
                .OrderByDescending(grouping => grouping.Key.DotNetVersion)
                .ThenBy(grouping => grouping.Key.OsVersion);
            foreach (var versionGrouping in versionGroups)
            {
                LegInfo leg = new LegInfo() { Name = $"{versionGrouping.Key.DotNetVersion}-{versionGrouping.Key.OsVersion}" };
                leg.Variables.Add(("dotnetVersion", versionGrouping.Key.DotNetVersion));
                leg.Variables.Add(("osVersion", versionGrouping.Key.OsVersion));
                matrix.Legs.Add(leg);
            }
        }

        private static void EmitVstsVariables(IEnumerable<MatrixInfo> matrices)
        {
            // Emit the special syntax to set a VSTS build definition matrix variable
            // ##vso[task.setvariable variable=x;isoutput=true]{ \"a\": { \"v1\": \"1\" }, \"b\": { \"v1\": \"2\" } }
            foreach (MatrixInfo matrix in matrices)
            {
                string legs = matrix.Legs
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
        /// Formats a build leg name from the specified Dockerfile path. Any parts of the Dockerfile path that are in common with the
        /// containing matrix name are trimmed. The resulting leg name uses '-' characters as word separators.
        /// </summary>
        private static string FormatLegName(string[] dockerfilePath, string[] matrixNameParts)
        {
            string legName = dockerfilePath.First().Split(PathSeparators)
                .Where(subPart => !matrixNameParts.Any(matrixPart => string.Equals(matrixPart, subPart, StringComparison.OrdinalIgnoreCase)))
                .Aggregate((working, next) => $"{working}-{next}");

            if (dockerfilePath.Length > 1)
            {
                legName += "-graph";
            }

            return legName;
        }

        /// <summary>
        /// Formats a matrix name by joining the specified parts. The resulting matrix name is camelCased.
        /// Any '-' occurrences within the specified parts will be treated as word boundaries.
        /// </summary>
        private static string FormatMatrixName(string[] parts)
        {
            string[] allParts = parts.SelectMany(part => part.Split('-')).ToArray();
            return allParts.First() +
                string.Join(string.Empty, allParts.Skip(1).Select(part => char.ToUpper(part[0]) + part.Substring(1)));
        }

        private IEnumerable<MatrixInfo> GenerateMatrixInfo()
        {
            List<MatrixInfo> matrices = new List<MatrixInfo>();

            var platformGroups = Manifest.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(image => image.Platforms)
                .GroupBy(platform => new { platform.Model.OS, platform.Model.OsVersion, platform.Model.Architecture })
                .OrderBy(platformGroup => platformGroup.Key.OS)
                .ThenByDescending(platformGroup => platformGroup.Key.OsVersion)
                .ThenBy(platformGroup => platformGroup.Key.Architecture);

            foreach (var platformGrouping in platformGroups)
            {
                string[] matrixNameParts =
                {
                    $"{Options.MatrixType.ToString().ToLowerInvariant()}Matrix",
                    platformGrouping.Key.OS == OS.Windows ? platformGrouping.Key.OsVersion : platformGrouping.Key.OS.ToString(),
                    platformGrouping.Key.Architecture.GetDisplayName(useLongNames: true)
                };
                MatrixInfo matrix = new MatrixInfo() { Name = FormatMatrixName(matrixNameParts) };
                matrices.Add(matrix);

                switch (Options.MatrixType)
                {
                    case MatrixType.Build:
                        AddBuildLegs(matrix, matrixNameParts, platformGrouping);
                        break;
                    case MatrixType.Test:
                        AddTestLegs(matrix, matrixNameParts, platformGrouping);
                        break;
                }
            }

            return matrices;
        }

        private IEnumerable<PlatformInfo> GetPlatformDependencies(PlatformInfo platform)
        {
            return platform.FromImages
                .Where(fromImage => !Manifest.IsExternalImage(fromImage))
                .Select(fromImage => Manifest.GetPlatformByTag(fromImage));
        }

        private void LogDiagnostics(IEnumerable<MatrixInfo> matrices)
        {
            if (Options.IsVerbose)
            {
                // Write out the matrices in a human friendly format
                foreach (MatrixInfo matrix in matrices)
                {
                    Logger.WriteMessage($"  {matrix.Name}:");
                    foreach (LegInfo leg in matrix.Legs)
                    {
                        Logger.WriteMessage($"    {leg.Name}:");
                        foreach (var variable in leg.Variables)
                        {
                            Logger.WriteMessage($"      {variable.Name}: {variable.Value}");
                        }
                    }
                }
            }
        }

        private class MatrixInfo
        {
            public string Name { get; set; }
            public List<LegInfo> Legs { get; } = new List<LegInfo>();
        }

        private class LegInfo
        {
            public string Name { get; set; }
            public List<(string Name, string Value)> Variables { get; } = new List<(string, string)>();
        }
    }
}
