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
        private List<ImageDocumentationInfo> ImageDocInfos { get; set; }

        public GenerateTagsReadmeCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING TAGS README");
            foreach (RepoInfo repo in Manifest.Repos)
            {
                ImageDocInfos = repo.Images
                    .SelectMany(image => image.Platforms.Select(platform => new ImageDocumentationInfo(image, platform)))
                    .Where(info => info.DocumentedTags.Any())
                    .ToList();

                string tagsDoc = Options.Template == null ?
                    GetManifestBasedDocumentation() : GetTemplateBasedDocumentation(Options.Template);

                Logger.WriteSubheading($"{repo.Name} Tags Documentation:");
                Logger.WriteMessage();
                Logger.WriteMessage(tagsDoc);

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

        private string GetManifestBasedDocumentation()
        {
            StringBuilder tagsDoc = new StringBuilder();

            var platformGroups = ImageDocInfos
                .GroupBy(info => new {info.Platform.Model.OS, info.Platform.Model.OsVersion, info.Platform.Model.Architecture })
                .OrderByDescending(platformGroup => platformGroup.Key.Architecture)
                .ThenBy(platformGroup => platformGroup.Key.OS)
                .ThenByDescending(platformGroup => platformGroup.Key.OsVersion);

            foreach (var platformGroup in platformGroups)
            {
                string os = GetOsDisplayName(platformGroup.Key.OS, platformGroup.Key.OsVersion);
                string arch = GetArchitectureDisplayName(platformGroup.Key.Architecture);
                tagsDoc.AppendLine($"# {os} {arch} tags");
                tagsDoc.AppendLine();

                IEnumerable<string> tagLines = platformGroup
                    .Select(info => GetTagDocumentation(info))
                    .Where(doc => doc != null);
                tagsDoc.AppendLine(string.Join(Environment.NewLine, tagLines));
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
                    if (osVersion == null || osVersion.Contains("2016"))
                    {
                        displayName = "Windows Server 2016";
                    }
                    else
                    {
                        displayName = $"Windows Server, version {osVersion}";
                    }
                    break;
                default:
                    displayName = os.ToString();
                    break;
            }

            return displayName;
        }

        private string GetTagDocumentation(ImageDocumentationInfo info)
        {
            string tags = info.DocumentedTags
                .Select(tag => $"`{tag}`")
                .Aggregate((working, next) => $"{working}, {next}");
            string dockerfile = info.Platform.DockerfilePath.Replace('\\', '/');
            return $"- [{tags} (*{dockerfile}*)]({Options.SourceUrl}/{dockerfile})";
        }

        private string GetTemplateBasedDocumentation(string templatePath)
        {
            string template = File.ReadAllText(templatePath);
            string tagsDoc = Manifest.VariableHelper.SubstituteValues(template, GetVariableValue);

            if (ImageDocInfos.Any())
            {
                string missingTags = string.Join(Environment.NewLine, ImageDocInfos.Select(info => GetTagDocumentation(info)));
                throw new InvalidOperationException(
                    $"The following tags are not documented in the readme: {Environment.NewLine}{missingTags}");
            }

            return tagsDoc;
        }

        private string GetVariableValue(string variableType, string variableName)
        {
            string variableValue = null;

            if (string.Equals(variableType, VariableHelper.TagDocTypeId, StringComparison.Ordinal))
            {
                ImageDocumentationInfo info = ImageDocInfos
                    .FirstOrDefault(idi => idi.Platform.Tags.Any(tag => tag.Name == variableName));
                if (info != null)
                {
                    variableValue = GetTagDocumentation(info);
                    ImageDocInfos.Remove(info);
                }
            }
            else if (string.Equals(variableType, VariableHelper.TagDocListTypeId, StringComparison.Ordinal))
            {
                IEnumerable<string> tags = variableName.Split('|');
                if (tags.Any())
                {
                    ImageDocumentationInfo info = ImageDocInfos
                        .FirstOrDefault(idi => idi.DocumentedTags.Intersect(tags).Count() == tags.Count());
                    if (info != null)
                    {
                        variableValue = GetTagDocumentation(new ImageDocumentationInfo(info.Platform, tags));

                        // Remove the tags referenced by the TagDocList.  This will ensure an exception if there are any tags
                        // excluded from the readme.
                        info.DocumentedTags = info.DocumentedTags.Except(tags);
                        if (!info.DocumentedTags.Any())
                        {
                            ImageDocInfos.Remove(info);
                        }
                    }
                }
            }

            return variableValue;
        }

        private static string NormalizeLineEndings(string value, string targetFormat)
        {
            string targetLineEnding = targetFormat.Contains("\r\n") ? "\r\n" : "\n";
            string valueLineEnding = value.Contains("\r\n") ? "\r\n" : "\n";
            if (valueLineEnding != targetLineEnding)
            {
                value = value.Replace(valueLineEnding, targetLineEnding);
            }

            return value;
        }

        private static void UpdateReadme(string tagsDocumentation, RepoInfo repo)
        {
            Logger.WriteHeading("UPDATING README");

            string readme = File.ReadAllText(repo.Model.ReadmePath);

            // tagsDocumentation is formatted with Environment.NewLine which may not match the readme format. This can
            // happen when image-builder is invoked within a Linux container on a Windows host while using a host volume.
            // Normalize the line endings to match the readme.
            tagsDocumentation = NormalizeLineEndings(tagsDocumentation, readme);

            string updatedReadme = Regex.Replace(readme, "(([#*]+.*\\s*)(- \\[.*\\s*)+)+", tagsDocumentation);
            File.WriteAllText(repo.Model.ReadmePath, updatedReadme);

            Logger.WriteSubheading($"Updated '{repo.Model.ReadmePath}'");
            Logger.WriteMessage();
        }

        private class ImageDocumentationInfo
        {
            public PlatformInfo Platform { get; }
            public IEnumerable<string> DocumentedTags { get; set; }

            public ImageDocumentationInfo(ImageInfo image, PlatformInfo platform)
            {
                Platform = platform;
                DocumentedTags = GetDocumentedTags(Platform.Tags)
                    .Concat(GetDocumentedTags(image.SharedTags))
                    .ToArray();
            }

            public ImageDocumentationInfo(PlatformInfo platform, IEnumerable<string> documentedTags)
            {
                Platform = platform;
                DocumentedTags = documentedTags;
            }

            private static IEnumerable<string> GetDocumentedTags(IEnumerable<TagInfo> tagInfos)
            {
                return tagInfos.Where(tag => !tag.Model.IsUndocumented)
                    .Select(tag => tag.Name);
            }
        }
    }
}
