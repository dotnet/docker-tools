// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateTagsReadmeCommand : Command<GenerateTagsReadmeOptions>
    {
        public GenerateTagsReadmeCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Utilities.WriteHeading("GENERATING TAGS README");
            foreach (RepoInfo repo in Manifest.Repos)
            {
                string tagsDoc = GetTagsDocumentation(repo);

                Console.WriteLine($"-- {repo.Name} Tags Documentation:");
                Console.WriteLine();
                Console.WriteLine(tagsDoc);

                if (Options.UpdateReadme)
                {
                    UpdateReadme(tagsDoc, repo);
                }
            }

            return Task.CompletedTask;
        }

        private static string GetArchitectureDisplayName(Architecture architecture)
        {
            string displayName;

            switch (architecture)
            {
                case Architecture.ARM:
                    displayName = "arm32";
                    break;
                default:
                    displayName = architecture.ToString().ToLowerInvariant();
                    break;
            }

            return displayName;
        }

        private static IEnumerable<string> GetDocumentedTags(IEnumerable<TagInfo> tagInfos)
        {
            return tagInfos.Where(tag => !tag.Model.IsUndocumented)
                .Select(tag => tag.Name);
        }

        private string GetTagsDocumentation(RepoInfo repo)
        {
            StringBuilder tagsDoc = new StringBuilder();

            var platformGroups = repo.Images
                .OrderBy(image => image.Model.ReadmeOrder)
                .SelectMany(image => image.Platforms.Select(platform => new { Image = image, Platform = platform }))
                .GroupBy(tuple => new { tuple.Platform.Model.OS, tuple.Platform.Model.OsVersion, tuple.Platform.Model.Architecture })
                .OrderByDescending(platformGroup => platformGroup.Key.Architecture)
                .ThenBy(platformGroup => platformGroup.Key.OS)
                .ThenByDescending(platformGroup => platformGroup.Key.OsVersion);

            foreach (var platformGroup in platformGroups)
            {
                string os = GetOsDisplayName(platformGroup.Key.OS, platformGroup.Key.OsVersion);
                string arch = GetArchitectureDisplayName(platformGroup.Key.Architecture);
                tagsDoc.AppendLine($"# Supported {os} {arch} tags");
                tagsDoc.AppendLine();
                foreach (var tuple in platformGroup)
                {
                    string tags = GetDocumentedTags(tuple.Platform.Tags)
                        .Concat(GetDocumentedTags(tuple.Image.SharedTags))
                        .Select(tag => $"`{tag}`")
                        .Aggregate((working, next) => $"{working}, {next}");
                    string dockerfile = tuple.Platform.DockerfilePath.Replace('\\', '/');
                    tagsDoc.AppendLine($"- [{tags} (*{dockerfile}*)]({Options.SourceUrl}/{dockerfile})");
                }

                tagsDoc.AppendLine();
            }

            return tagsDoc.ToString();
        }

        private static string GetOsDisplayName(OS os, string osVersion)
        {
            string displayName;

            switch (os)
            {
                case OS.Windows:
                    displayName = "Windows Server 2016";

                    if (osVersion != null && (osVersion.Contains("1709") || osVersion.Contains("16299")))
                    {
                        displayName += " Version 1709 (Fall Creators Update)";
                    }

                    break;
                default:
                    displayName = os.ToString();
                    break;
            }

            return displayName;
        }

        private void UpdateReadme(string tagsDocumentation, RepoInfo repo)
        {
            Utilities.WriteHeading("UPDATING README");

            string readme = File.ReadAllText(repo.Model.ReadmePath);
            string updatedReadme = Regex.Replace(readme, "(# .*\\s*(- \\[.*\\s*)+)+", tagsDocumentation);
            File.WriteAllText(repo.Model.ReadmePath, updatedReadme);

            Console.WriteLine($"-- Updated '{repo.Model.ReadmePath}'");
            Console.WriteLine();
        }
    }
}
