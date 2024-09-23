// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.McrTags;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.DotNet.ImageBuilder
{
    public class McrTagsMetadataGenerator
    {
        private IGitService _gitService;
        private ManifestInfo _manifest;
        private RepoInfo _repo;
        private string _sourceRepoUrl;
        private string _sourceBranch;
        private List<ImageDocumentationInfo> _imageDocInfos;
        private bool _useRelativeLinks;

        public static string Execute(
            IGitService gitService,
            ManifestInfo manifest,
            RepoInfo repo,
            string sourceRepoUrl,
            string sourceBranch = null,
            bool useRelativeLinks = false)
        {
            McrTagsMetadataGenerator generator = new()
            {
                _gitService = gitService,
                _manifest = manifest,
                _repo = repo,
                _sourceRepoUrl = sourceRepoUrl,
                _sourceBranch = sourceBranch,
                _useRelativeLinks = useRelativeLinks,
            };

            return generator.Execute();
        }

        private string Execute()
        {
            Logger.WriteHeading("GENERATING MCR TAGS METADATA");

            _imageDocInfos = _repo.FilteredImages
                .SelectMany(image =>
                    image.AllPlatforms.SelectMany(platform => ImageDocumentationInfo.Create(image, platform)))
                .Where(info => info.DocumentedTags.Any())
                .ToList();

            StringBuilder yaml = new StringBuilder();
            yaml.AppendLine("repos:");

            string templatePath = Path.Combine(_manifest.Directory, _repo.Model.McrTagsMetadataTemplate);

            string template = File.ReadAllText(templatePath);
            yaml.Append(_manifest.VariableHelper.SubstituteValues(template, GetVariableValue));

            if (_imageDocInfos.Any())
            {
                string missingTags = string.Join(
                    Environment.NewLine, _imageDocInfos.Select(info => info.FormattedDocumentedTags));
                throw new InvalidOperationException(
                    $"The following tags are not included in the tags metadata: {Environment.NewLine}{missingTags}");
            }

            string metadata = yaml.ToString();

            Logger.WriteSubheading("Generated Metadata:");
            Logger.WriteMessage(metadata);

            // Validate that the YAML is in a valid format
            new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<TagsMetadata>(metadata);

            return metadata;
        }

        private static string GetRepoYaml(RepoInfo repo)
        {
            StringBuilder yaml = new StringBuilder();
            yaml.AppendLine($"- repoName: public/{repo.Name}");
            yaml.AppendLine("  customTablePivots: true");
            yaml.Append("  tagGroups:");

            return yaml.ToString();
        }

        private string GetTagGroupYaml(IEnumerable<ImageDocumentationInfo> infos)
        {
            ImageDocumentationInfo firstInfo = infos.First();

            string dockerfilePath = _useRelativeLinks
                ? firstInfo.Platform.DockerfilePathRelativeToManifest
                : _gitService.GetDockerfileCommitUrl(firstInfo.Platform, _sourceRepoUrl, _sourceBranch);

            // Generate a list of tags that have this sorting convention:
            // <concrete tags>, <shared tags of platforms that have no concrete tags>, <shared tags of platforms that have concrete tags>
            // This convention should produce a list of tags that are listed from most specificity to least.

            IEnumerable<string> formattedPlatformTags = infos
                .SelectMany(info => info.DocumentedPlatformTags.Select(tag => tag.Name));

            IEnumerable<string> formattedSharedTags = infos
                .Where(info => !info.DocumentedPlatformTags.Any())
                .SelectMany(info => info.DocumentedSharedTags.Select(tag => tag.Name));

            formattedSharedTags = formattedSharedTags.Concat(infos
                .Where(info => info.DocumentedPlatformTags.Any())
                .SelectMany(info => info.DocumentedSharedTags.Select(tag => tag.Name)));

            string formattedTags = string.Join(", ", formattedPlatformTags.Concat(formattedSharedTags));

            StringBuilder yaml = new StringBuilder();
            yaml.AppendLine($"  - tags: [ {formattedTags} ]");
            yaml.AppendLine($"    architecture: {firstInfo.Platform.Model.Architecture.GetDisplayName()}");
            yaml.AppendLine($"    os: {firstInfo.Platform.Model.OS.GetDockerName()}");
            yaml.AppendLine($"    osVersion: {firstInfo.Platform.GetOSDisplayName()}");
            yaml.Append($"    dockerfile: {dockerfilePath}");

            return yaml.ToString();
        }

        private string GetVariableValue(string variableType, string variableName)
        {
            string variableValue = null;

            if (string.Equals(variableType, VariableHelper.McrTagsYmlRepoTypeId, StringComparison.Ordinal))
            {
                RepoInfo repo = _manifest.GetFilteredRepoById(variableName);
                variableValue = GetRepoYaml(repo);
            }
            else if (string.Equals(variableType, VariableHelper.McrTagsYmlTagGroupTypeId, StringComparison.Ordinal))
            {
                ImageDocumentationInfo info = _imageDocInfos
                    .FirstOrDefault(idi => idi.DocumentedTags.Any(tag => tag.Name == variableName));
                if (info != null)
                {
                    // Find all other doc infos that match this one. This accounts for scenarios where a platform is
                    // duplicated in another image in order to associate it within a distinct set of shared tags.
                    IEnumerable<ImageDocumentationInfo> matchingDocInfos = _imageDocInfos
                        .Where(docInfo => docInfo.Platform != info.Platform &&
                            PlatformInfo.AreMatchingPlatforms(docInfo.Image, docInfo.Platform, info.Image, info.Platform))
                        .Prepend(info)
                        .ToArray();

                    foreach (ImageDocumentationInfo docInfo in matchingDocInfos)
                    {
                        _imageDocInfos.Remove(docInfo);
                    }

                    variableValue = GetTagGroupYaml(matchingDocInfos);
                }
            }

            return variableValue;
        }

        private class ImageDocumentationInfo
        {
            public IEnumerable<TagInfo> DocumentedTags { get; }
            public string FormattedDocumentedTags { get; }
            public PlatformInfo Platform { get; }
            public ImageInfo Image { get; }
            public IEnumerable<TagInfo> DocumentedPlatformTags { get; }
            public IEnumerable<TagInfo> DocumentedSharedTags { get; }

            private ImageDocumentationInfo(ImageInfo image, PlatformInfo platform, string documentationGroup)
            {
                Image = image;
                Platform = platform;
                DocumentedPlatformTags = GetDocumentedTags(Platform.Tags, documentationGroup).ToArray();
                DocumentedSharedTags = GetDocumentedTags(image.SharedTags, documentationGroup, DocumentedPlatformTags);
                DocumentedTags = DocumentedPlatformTags
                    .Concat(DocumentedSharedTags)
                    .ToArray();
                FormattedDocumentedTags = string.Join(
                    ", ",
                    DocumentedTags
                        .Select(tag => tag.Name)
                        .ToArray());
            }

            public static IEnumerable<ImageDocumentationInfo> Create(ImageInfo image, PlatformInfo platform)
            {
                IEnumerable<string> documentationGroups = image.SharedTags
                    .Concat(platform.Tags)
                    .Select(tag => tag.Model.DocumentationGroup)
                    .Distinct();
                foreach (string documentationGroup in documentationGroups)
                {
                    yield return new ImageDocumentationInfo(image, platform, documentationGroup);
                }
            }

            private static IEnumerable<TagInfo> GetDocumentedTags(
                IEnumerable<TagInfo> tagInfos, string documentationGroup, IEnumerable<TagInfo> documentedPlatformTags = null) =>
                    tagInfos.Where(tag =>
                        tag.Model.DocumentationGroup == documentationGroup &&
                            (tag.Model.DocType == TagDocumentationType.Documented ||
                                (tag.Model.DocType == TagDocumentationType.PlatformDocumented && documentedPlatformTags?.Any() == true)));
        }
    }
}
