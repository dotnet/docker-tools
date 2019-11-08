// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IMcrTagsMetadataGenerator))]
    public class McrTagsMetadataGenerator : IMcrTagsMetadataGenerator
    {
        private readonly IGitService gitService;

        [ImportingConstructor]
        public McrTagsMetadataGenerator(IGitService gitService)
        {
            this.gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        }

        public string Execute(ManifestInfo manifest, RepoInfo repo, string sourceRepoUrl, string sourceBranch = null)
        {
            return new McrTagsMetadataGeneratorImpl(gitService, manifest, repo, sourceRepoUrl, sourceBranch).Execute();
        }

        private class McrTagsMetadataGeneratorImpl
        {
            private readonly IGitService _gitService;
            private readonly ManifestInfo _manifest;
            private readonly RepoInfo _repo;
            private readonly string _sourceRepoUrl;
            private readonly string _sourceBranch;
            private List<ImageDocumentationInfo> _imageDocInfos;

            public McrTagsMetadataGeneratorImpl(
                IGitService gitService,
                ManifestInfo manifest,
                RepoInfo repo,
                string sourceRepoUrl,
                string sourceBranch)
            {
                _gitService = gitService;
                _manifest = manifest;
                _repo = repo;
                _sourceRepoUrl = sourceRepoUrl;
                _sourceBranch = sourceBranch;
            }

            public string Execute()
            {
                Logger.WriteHeading("GENERATING MCR TAGS METADATA");

                _imageDocInfos = _repo.FilteredImages
                    .SelectMany(image =>
                        image.AllPlatforms.SelectMany(platform => ImageDocumentationInfo.Create(image, platform)))
                    .Where(info => info.DocumentedTags.Any())
                    .ToList();

                StringBuilder yaml = new StringBuilder();
                yaml.AppendLine("repos:");

                string template = File.ReadAllText(_repo.Model.McrTagsMetadataTemplatePath);
                yaml.Append(_manifest.VariableHelper.SubstituteValues(template, GetVariableValue));

                if (_imageDocInfos.Any())
                {
                    string missingTags = string.Join(
                        Environment.NewLine, _imageDocInfos.Select(info => info.FormattedDocumentedTags));
                    throw new InvalidOperationException(
                        $"The following tags are not included in the tags metadata: {Environment.NewLine}{missingTags}");
                }

                string metadata = yaml.ToString();

                // Validate that the YAML is in a valid format
                new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build()
                    .Deserialize<Models.Mcr.McrTagsMetadata>(metadata);

                Logger.WriteSubheading("Generated Metadata:");
                Logger.WriteMessage(metadata);

                return metadata;
            }

            public static string GetOSDisplayName(PlatformInfo platform)
            {
                string displayName;
                string os = platform.Model.OsVersion;
                Logger.WriteMessage($"os: {os}");
                Logger.WriteMessage($"osType: {platform.Model.OS}");

                if (platform.Model.OS == OS.Windows)
                {
                    if (os.Contains("2016"))
                    {
                        displayName = "Windows Server 2016";
                    }
                    else if (os.Contains("2019") || os.Contains("1809"))
                    {
                        displayName = "Windows Server 2019";
                    }
                    else
                    {
                        string version = os.Split('-')[1];
                        displayName = $"Windows Server, version {version}";
                    }
                }
                else
                {
                    if (os.Contains("jessie"))
                    {
                        displayName = "Debian 8";
                    }
                    else if (os.Contains("stretch"))
                    {
                        displayName = "Debian 9";
                    }
                    else if (os.Contains("buster"))
                    {
                        displayName = "Debian 10";
                    }
                    else if (os.Contains("bionic"))
                    {
                        displayName = "Ubuntu 18.04";
                    }
                    else if (os.Contains("disco"))
                    {
                        displayName = "Ubuntu 19.04";
                    }
                    else if (os.Contains("alpine"))
                    {
                        int versionIndex = os.IndexOfAny(new char[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' });
                        if (versionIndex != -1)
                        {
                            os = os.Insert(versionIndex, " ");
                        }

                        displayName = os.FirstCharToUpper();
                    }
                    else
                    {
                        throw new InvalidOperationException($"The OS version '{os}' is not supported.");
                    }
                }

                return displayName;
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
                string dockerfileRelativePath = info.Platform.DockerfilePath.Replace('\\', '/');
                string branchOrShaPathSegment = _sourceBranch ??
                    _gitService.GetCommitSha(dockerfileRelativePath, useFullHash: true);
                string dockerfilePath = $"{_sourceRepoUrl}/blob/{branchOrShaPathSegment}/{dockerfileRelativePath}";

                StringBuilder yaml = new StringBuilder();
                yaml.AppendLine($"  - tags: [ {info.FormattedDocumentedTags} ]");
                yaml.AppendLine($"    architecture: {info.Platform.Model.Architecture.GetDisplayName()}");
                yaml.AppendLine($"    os: {info.Platform.Model.OS.GetDockerName()}");
                yaml.AppendLine($"    osVersion: {GetOSDisplayName(info.Platform)}");
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
}
