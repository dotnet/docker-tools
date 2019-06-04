// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ImageBuilder.ManifestModel;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class McrTagsMetadataGenerator
    {
        private List<ImageDocumentationInfo> _imageDocInfos;
        private ManifestInfo _manifest;
        private RepoInfo _repo;
        private string _sourceUrl;

        private McrTagsMetadataGenerator()
        {
        }

        public static string Execute(ManifestInfo manifest, RepoInfo repo, string sourceUrl)
        {
            McrTagsMetadataGenerator generator = new McrTagsMetadataGenerator()
            {
                _manifest = manifest,
                _repo = repo,
                _sourceUrl = sourceUrl,
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
            StringBuilder yaml = new StringBuilder();
            yaml.AppendLine($"  - tags: [ {info.FormattedDocumentedTags} ]");
            yaml.AppendLine($"    architecture: {info.Platform.Model.Architecture.GetDisplayName()}");
            yaml.AppendLine($"    os: {info.Platform.Model.OS.GetDockerName()}");
            yaml.AppendLine($"    osVersion: {GetOSDisplayName(info.Platform)}");
            yaml.Append($"    dockerfile: {_sourceUrl}/{info.Platform.DockerfilePath.Replace('\\', '/')}");

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
