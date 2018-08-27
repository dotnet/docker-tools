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
    public class GenerateBuildQueueCommand : Command<GenerateBuildQueueOptions>
    {
        public GenerateBuildQueueCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING BUILD QUEUE");

            var platformGroups = Manifest.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(image => image.Platforms)
                .GroupBy(platform => new { platform.Model.OS, platform.Model.OsVersion, platform.Model.Architecture })
                .OrderBy(platformGroup => platformGroup.Key.OS)
                .ThenByDescending(platformGroup => platformGroup.Key.OsVersion)
                .ThenBy(platformGroup => platformGroup.Key.Architecture);

            foreach (var platformGrouping in platformGroups)
            {
                string[] queueNameParts = 
                {
                    "buildMatrix",
                    platformGrouping.Key.OS == OS.Windows ? platformGrouping.Key.OsVersion : platformGrouping.Key.OS.ToString(),
                    platformGrouping.Key.Architecture.GetDisplayName(useLongNames: true)
                };
                Logger.WriteMessage($"  {FormatQueueName(queueNameParts)}:");

                // Emit legs and their variables
                IEnumerable<IEnumerable<PlatformInfo>> subgraphs = platformGrouping.GetCompleteSubgraphs(GetPlatformDependencies);
                foreach (IEnumerable<PlatformInfo> subgraph in subgraphs)
                {
                    string[] dockerfilePaths = subgraph
                        .Select(platform => platform.Model.Dockerfile)
                        .ToArray();
                    Logger.WriteMessage($"    {FormatLegName(dockerfilePaths, queueNameParts)}:");

                    string pathArgs = dockerfilePaths
                        .Select(path => $"--path {path}")
                        .Aggregate((working, next) => $"{working} {next}");
                    Logger.WriteMessage($"      imageBuilderPaths: {pathArgs}");
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Formats a build leg name from the specified Dockerfile path. Any parts of the Dockerfile path that are in common with the
        /// containing queue name are trimmed. The resulting leg name uses '-' characters as word separators.
        /// </summary>
        private static string FormatLegName(string[] dockerfilePath, string[] queueNameParts)
        {
            string legName = dockerfilePath.First().Split('/')
                .Where(subPart => !queueNameParts.Any(queuePart => string.Equals(queuePart, subPart, StringComparison.OrdinalIgnoreCase)))
                .Aggregate((working, next) => $"{working}-{next}");

            if (dockerfilePath.Length > 1)
            {
                legName += "-graph";
            }

            return legName;
        }

        /// <summary>
        /// Formats a queue name by joining the specified parts. The resulting queue name is camelCased.
        /// Any '-' occurrences within the specified parts will be treated as word boundaries.
        /// </summary>
        private static string FormatQueueName(string[] parts)
        {
            string[] allParts = parts.SelectMany(part => part.Split('-')).ToArray();
            return allParts.First() +
                string.Join(string.Empty, allParts.Skip(1).Select(part => char.ToUpper(part[0]) + part.Substring(1)));
        }

        private IEnumerable<PlatformInfo> GetPlatformDependencies(PlatformInfo platform)
        {
            return platform.FromImages
                .Where(fromImage => !Manifest.IsExternalImage(fromImage))
                .Select(fromImage => Manifest.GetPlatformByTag(fromImage));
        }
    }
}
