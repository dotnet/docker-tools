#nullable disable
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

namespace Microsoft.DotNet.ImageBuilder.Mcr
{
    public class McrTagsMetadataGenerator
    {
        private static readonly ILogger Logger = StandaloneLoggerFactory.CreateLogger<McrTagsMetadataGenerator>();

        private IGitService _gitService;
        private ManifestInfo _manifest;
        private RepoInfo _repo;
        private string _sourceRepoUrl;
        private string _sourceBranch;
        private List<ImageDocumentationInfo> _imageDocInfos;
        private bool _generateGitHubLinks;

        private static readonly Dictionary<Architecture, int> s_archSortKeys = new() {
            { Architecture.AMD64, 0 },
            { Architecture.ARM64, 1 },
            { Architecture.ARM, 2 }
        };

        public static string Execute(
            ManifestInfo manifest,
            RepoInfo repo,
            bool generateGitHubLinks = false,
            IGitService gitService = null,
            string sourceRepoUrl = null,
            string sourceBranch = null)
        {
            // Generating GitHub permalinks requires gitService
            if (generateGitHubLinks == true)
            {
                ArgumentNullException.ThrowIfNull(gitService);
            }

            McrTagsMetadataGenerator generator = new()
            {
                _manifest = manifest,
                _repo = repo,
                _generateGitHubLinks = generateGitHubLinks,
                _gitService = gitService,
                _sourceRepoUrl = sourceRepoUrl,
                _sourceBranch = sourceBranch,
            };

            return generator.Execute();
        }

        private string Execute()
        {
            Logger.LogInformation("GENERATING MCR TAGS METADATA");

            _imageDocInfos = _repo.FilteredImages
                .SelectMany(image => image.AllPlatforms
                    .Select(platform => new ImageDocumentationInfo(image, platform)))
                .ToList();

            StringBuilder yaml = new StringBuilder();
            yaml.AppendLine("repos:");

            string templatePath = Path.Combine(_manifest.Directory, _repo.Model.McrTagsMetadataTemplate);
            string template = File.ReadAllText(templatePath);

            // GetVariableValue removes imageDocInfos from the list as they are used,
            // leaving us with a list of imageDocInfos that were not used in metadata generation
            yaml.Append(_manifest.VariableHelper.SubstituteValues(template, GetVariableValue));

            IReadOnlyList<ImageDocumentationInfo> missingTags = _imageDocInfos
                .Where(docInfo => docInfo.DocumentedTags.Any())
                .ToList();

            if (missingTags.Count > 0)
            {
                IEnumerable<string> missingTagsPerImage = missingTags.Select(imageDocInfo =>
                    $"""
                    Repo: {_repo.Name}, Platform: {imageDocInfo.Platform.GetOSDisplayName()} {imageDocInfo.Platform.Model.Architecture}
                    Missing Tags: {imageDocInfo.FormattedDocumentedTags}
                    """);

                string missingTagsString = string.Join(Environment.NewLine, missingTagsPerImage);

                throw new InvalidOperationException(
                    $"The following tags are not included in the tags metadata: {Environment.NewLine}{missingTagsString}{Environment.NewLine}");
            }

            string metadata = yaml.ToString();

            Logger.LogInformation("Generated Metadata:");
            Logger.LogInformation(metadata);

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

        private string GetTagGroupYaml(IEnumerable<ImageDocumentationInfo> infos, string customSubTableTitle)
        {
            ImageDocumentationInfo firstInfo = infos.First();

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
            yaml.Append($"    dockerfile: {GetDockerfilePath(firstInfo)}");

            if (!string.IsNullOrWhiteSpace(customSubTableTitle))
            {
                yaml.AppendLine();
                yaml.Append($"    customSubTableTitle: {customSubTableTitle}");
            }

            return yaml.ToString();
        }

        private string GetDockerfilePath(ImageDocumentationInfo firstInfo) => _generateGitHubLinks
            ? _gitService.GetDockerfileCommitUrl(firstInfo.Platform, _sourceRepoUrl, _sourceBranch)
            : firstInfo.Platform.DockerfilePathRelativeToManifest;

        private string GetVariableValue(string variableType, string variableName)
        {
            if (string.Equals(variableType, VariableHelper.McrTagsYmlRepoTypeId, StringComparison.Ordinal))
            {
                RepoInfo repo = _manifest.GetFilteredRepoById(variableName);
                return GetRepoYaml(repo);
            }

            if (string.Equals(variableType, VariableHelper.McrTagsYmlTagGroupTypeId, StringComparison.Ordinal))
            {
                // Custom tag tables can be specified by inserting a `|` delimiter. Specifying it inline with the
                // variable ensures that it gets applied to all architectures of any multi-arch tags.
                string[] variableParts = variableName.Split('|', 2);
                string thisTag = variableParts[0];
                string customSubTableTitle = variableParts.Length == 2 ? variableParts[1] : "";

                // Check if we're dealing with a multi-arch linux tag by looking at all shared tags and seeing if any match.
                IEnumerable<ImageDocumentationInfo> matchingSharedTags = _imageDocInfos
                    .Where(imageDocInfo => !imageDocInfo.Platform.IsWindows)
                    .Where(imageDocInfo => imageDocInfo.SharedTags.Any(tagInfo => tagInfo.Name == thisTag));

                // If the tag is multi-arch, add a separate tag group yaml for each architecture that it refers to.
                if (matchingSharedTags.Any())
                {
                    var yaml = new StringBuilder();

                    // Find all other doc infos that match this one. This accounts for scenarios where a platform is
                    // duplicated in another image in order to associate it within a distinct set of shared tags.
                    matchingSharedTags = matchingSharedTags.SelectMany(GetMatchingDocInfos);

                    // Sort the matching shared tags by architecture since we can't specify the order in the template
                    // when using multi-arch (shared) tags.
                    matchingSharedTags = matchingSharedTags
                        .Distinct()
                        .OrderBy(docInfo =>
                            s_archSortKeys.GetValueOrDefault(docInfo.Platform.Model.Architecture, int.MaxValue));

                    List<IGrouping<string, ImageDocumentationInfo>> platformGroups = matchingSharedTags
                        .GroupBy(imageDocInfo => imageDocInfo.Platform.GetUniqueKey(imageDocInfo.Image))
                        .ToList();

                    platformGroups.ForEach(imageDocInfos =>
                        yaml.AppendLine(GetTagGroupYaml(imageDocInfos, customSubTableTitle)));

                    // Remove used imageDocInfos from the list
                    foreach(ImageDocumentationInfo imageDocInfo in matchingSharedTags)
                    {
                        _imageDocInfos.Remove(imageDocInfo);
                    }

                    return yaml.ToString().TrimEndString(Environment.NewLine);
                }

                // Otherwise, get the single platform tag that matches here
                ImageDocumentationInfo info = _imageDocInfos
                    .FirstOrDefault(idi => idi.PlatformTags.Any(tagInfo => tagInfo.Name == thisTag));

                if (info is null)
                {
                    return null;
                }

                // Find all other doc infos that match this one. This accounts for scenarios where a platform is
                // duplicated in another image in order to associate it within a distinct set of shared tags.
                IEnumerable<ImageDocumentationInfo> matchingDocInfos = GetMatchingDocInfos(info);

                foreach (ImageDocumentationInfo docInfo in matchingDocInfos)
                {
                    _imageDocInfos.Remove(docInfo);
                }

                return GetTagGroupYaml(matchingDocInfos, customSubTableTitle);
            }

            return null;
        }

        private List<ImageDocumentationInfo> GetMatchingDocInfos(ImageDocumentationInfo info) =>
            _imageDocInfos
                .Where(docInfo => !ReferenceEquals(docInfo.Platform, info.Platform)
                    && PlatformInfo.AreMatchingPlatforms(docInfo.Image, docInfo.Platform, info.Image, info.Platform))
                .Prepend(info)
                .ToList();

        #nullable enable
        private class ImageDocumentationInfo
        {
            public PlatformInfo Platform { get; }
            public ImageInfo Image { get; }
            public IEnumerable<TagInfo> SharedTags { get; }
            public IEnumerable<TagInfo> PlatformTags { get; }
            public IEnumerable<TagInfo> AllTags { get; }
            public IEnumerable<TagInfo> DocumentedSharedTags { get; }
            public IEnumerable<TagInfo> DocumentedPlatformTags { get; }
            public IEnumerable<TagInfo> DocumentedTags { get; }
            public string FormattedDocumentedTags { get; }

            public ImageDocumentationInfo(ImageInfo image, PlatformInfo platform)
            {
                Image = image;
                Platform = platform;

                SharedTags = Image.SharedTags;
                PlatformTags = Platform.Tags;
                AllTags = [..PlatformTags, ..SharedTags];

                DocumentedPlatformTags = PlatformTags.Where(TagIsDocumented);
                DocumentedSharedTags = SharedTags.Where(tag =>
                    TagIsDocumented(tag) || TagIsPlatformDocumented(tag, DocumentedPlatformTags));
                DocumentedTags = [..DocumentedPlatformTags, ..DocumentedSharedTags];

                FormattedDocumentedTags = string.Join(", ", DocumentedTags.Select(tag => tag.Name));
            }

            private static bool TagIsDocumented(TagInfo tag) =>
                tag.Model.DocType == TagDocumentationType.Documented;

            private static bool TagIsPlatformDocumented(TagInfo tag, IEnumerable<TagInfo> documentedPlatformTags) =>
                tag.Model.DocType == TagDocumentationType.PlatformDocumented
                    && documentedPlatformTags?.Any() == true;
        }
        #nullable disable
    }
}
