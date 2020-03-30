// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
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

        public static string Execute(IGitService gitService, ManifestInfo manifest, RepoInfo repo, string sourceRepoUrl, string sourceBranch = null)
        {
            McrTagsMetadataGenerator generator = new McrTagsMetadataGenerator()
            {
                _gitService = gitService,
                _manifest = manifest,
                _repo = repo,
                _sourceRepoUrl = sourceRepoUrl,
                _sourceBranch = sourceBranch,
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

            string templatePath = Path.Combine(_manifest.Directory, _repo.Model.McrTagsMetadataTemplatePath);

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
                .Deserialize<Models.Mcr.McrTagsMetadata>(metadata);

            return metadata;
        }

        private static string GetRepoYaml(RepoInfo repo)
        {
            StringBuilder yaml = new StringBuilder();
            yaml.AppendLine($"- repoName: public/{repo.Model.Name}");
            yaml.AppendLine("  customTablePivots: true");
            yaml.Append("  tagGroups:");

            return yaml.ToString();
        }

        private string GetTagGroupYaml(ImageDocumentationInfo info)
        {
            string dockerfilePath = _gitService.GetDockerfileCommitUrl(info.Platform, _sourceRepoUrl, _sourceBranch);

            StringBuilder yaml = new StringBuilder();
            yaml.AppendLine($"  - tags: [ {info.FormattedDocumentedTags} ]");
            yaml.AppendLine($"    architecture: {info.Platform.Model.Architecture.GetDisplayName()}");
            yaml.AppendLine($"    os: {info.Platform.Model.OS.GetDockerName()}");
            yaml.AppendLine($"    osVersion: {info.Platform.GetOSDisplayName()}");
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
                    _imageDocInfos.Remove(info);
                    variableValue = GetTagGroupYaml(info);
                }
            }

            return variableValue;
        }

        private class ImageDocumentationInfo
        {
            public IEnumerable<TagInfo> DocumentedTags { get; set; }
            public string FormattedDocumentedTags { get; set; }
            public PlatformInfo Platform { get; }

            private ImageDocumentationInfo(ImageInfo image, PlatformInfo platform, string documentationGroup)
            {
                Platform = platform;
                DocumentedTags = GetDocumentedTags(Platform.Tags, documentationGroup)
                    .Concat(GetDocumentedTags(image.SharedTags, documentationGroup))
                    .ToArray();
                FormattedDocumentedTags = DocumentedTags
                    .Select(tag => tag.Name)
                    .Aggregate((working, next) => $"{working}, {next}");
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

            private static IEnumerable<TagInfo> GetDocumentedTags(IEnumerable<TagInfo> tagInfos, string documentationGroup) =>
                tagInfos.Where(tag => !tag.Model.IsUndocumented && tag.Model.DocumentationGroup == documentationGroup);
        }
    }
}
