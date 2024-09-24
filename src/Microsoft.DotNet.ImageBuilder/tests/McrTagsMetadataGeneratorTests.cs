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
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Newtonsoft.Json;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class McrTagsMetadataGeneratorTests
    {
        /// <summary>
        /// Verfies the Dockerfile path is set correctly
        /// </summary>
        /// <remarks>
        /// If the source branch isn't set, the commit SHA of the Dockerfile will be used in the URL
        /// See https://github.com/dotnet/dotnet-docker/issues/1436
        /// </remarks>
        [Theory]
        [InlineData(true, "branch")]
        [InlineData(true, null)]
        [InlineData(false, "branch")]
        [InlineData(false, null)]
        public static void DockerfileLink(bool generateGitHubLinks, string sourceRepoBranch)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            const string SourceRepoUrl = "https://www.github.com/dotnet/dotnet-docker";
            const string RepoName = "repo";
            const string TagName = "tag";

            // Create Dockerfile
            string DockerfileDir = $"1.0/{RepoName}/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, DockerfileDir));
            string dockerfileRelativePath = DockerfileDir + '/' + "Dockerfile";
            string dockerfileFullPath = PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, dockerfileRelativePath));
            File.WriteAllText(dockerfileFullPath, "FROM base:tag");

            // Create MCR tags metadata template file
            StringBuilder tagsMetadataTemplateBuilder = new StringBuilder();
            tagsMetadataTemplateBuilder.AppendLine($"$(McrTagsYmlRepo:{RepoName})");
            tagsMetadataTemplateBuilder.Append($"$(McrTagsYmlTagGroup:{TagName})");
            string tagsMetadataTemplatePath = Path.Combine(tempFolderContext.Path, "tags.yaml");
            File.WriteAllText(tagsMetadataTemplatePath, tagsMetadataTemplateBuilder.ToString());

            string emptyFileName = "emptyFile.md";
            string emptyFilePath = Path.Combine(tempFolderContext.Path, emptyFileName);
            File.WriteAllText(emptyFilePath, string.Empty);

            // Create manifest
            Manifest manifest = ManifestHelper.CreateManifest(
                ManifestHelper.CreateRepo(RepoName,
                    new Image[]
                    {
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { TagName }))
                    },
                    readme: emptyFileName,
                    readmeTemplate: emptyFileName,
                    mcrTagsMetadataTemplate: Path.GetFileName(tagsMetadataTemplatePath))
            );
            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            // Load manifest
            IManifestOptionsInfo manifestOptions = ManifestHelper.GetManifestOptions(manifestPath);
            ManifestInfo manifestInfo = ManifestInfo.Load(manifestOptions);
            RepoInfo repo = manifestInfo.AllRepos.First();

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            const string DockerfileSha = "random_sha";

            if (sourceRepoBranch == null)
            {
                gitServiceMock
                    .Setup(o => o.GetCommitSha(dockerfileFullPath, true))
                    .Returns(DockerfileSha);
            }

            // Execute generator
            string result = McrTagsMetadataGenerator.Execute(
                manifestInfo,
                repo,
                generateGitHubLinks: generateGitHubLinks,
                gitService: gitServiceMock.Object,
                sourceRepoUrl: SourceRepoUrl,
                sourceBranch: sourceRepoBranch);

            TagsMetadata tagsMetadata = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<TagsMetadata>(result);

            string branchOrSha = sourceRepoBranch ?? DockerfileSha;

            string expectedUrl = generateGitHubLinks
                ? $"{SourceRepoUrl}/blob/{branchOrSha}/{DockerfileDir}/Dockerfile"
                : dockerfileRelativePath;

            Assert.Equal(expectedUrl, tagsMetadata.Repos[0].TagGroups[0].Dockerfile);
        }

        /// <summary>
        /// Verifies that <see cref="McrTagsMetadataGenerator"/> can be run against a platform that has no
        /// documented tags.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void HandlesUndocumentedPlatform(bool hasSharedTag)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            const string SourceRepoUrl = "https://www.github.com/dotnet/dotnet-docker";
            const string RepoName = "repo";
            const string SourceBranch = "branch";

            // Create MCR tags metadata template file
            StringBuilder tagsMetadataTemplateBuilder = new StringBuilder();
            tagsMetadataTemplateBuilder.AppendLine($"$(McrTagsYmlRepo:{RepoName})");
            tagsMetadataTemplateBuilder.AppendLine($"$(McrTagsYmlTagGroup:tag1a)");
            string tagsMetadataTemplatePath = Path.Combine(tempFolderContext.Path, "tags.yaml");
            File.WriteAllText(tagsMetadataTemplatePath, tagsMetadataTemplateBuilder.ToString());

            string emptyFileName = "emptyFile.md";
            string emptyFilePath = Path.Combine(tempFolderContext.Path, emptyFileName);
            File.WriteAllText(emptyFilePath, string.Empty);

            Platform platform = ManifestHelper.CreatePlatform(
                DockerfileHelper.CreateDockerfile($"1.0/{RepoName}/os", tempFolderContext),
                Array.Empty<string>());
            platform.Tags = new Dictionary<string, Tag>
            {
                {
                    "tag2",
                    new Tag
                    {
                        DocType = TagDocumentationType.Undocumented
                    }
                }
            };

            Image image;

            // Create manifest
            Manifest manifest = ManifestHelper.CreateManifest(
                ManifestHelper.CreateRepo(RepoName,
                    new Image[]
                    {
                        image = ManifestHelper.CreateImage(
                            platform,
                            ManifestHelper.CreatePlatform(
                                DockerfileHelper.CreateDockerfile($"1.0/{RepoName}/os2", tempFolderContext),
                                new string[] { "tag1a", "tag1b" }))
                    },
                    readme: emptyFileName,
                    readmeTemplate: emptyFileName,
                    mcrTagsMetadataTemplate: Path.GetFileName(tagsMetadataTemplatePath))
            );

            if (hasSharedTag)
            {
                image.SharedTags = new Dictionary<string, Tag>
                {
                    { "shared", new Tag
                        {
                            DocType = TagDocumentationType.PlatformDocumented
                        }
                    }
                };
            }

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            // Load manifest
            IManifestOptionsInfo manifestOptions = ManifestHelper.GetManifestOptions(manifestPath);
            ManifestInfo manifestInfo = ManifestInfo.Load(manifestOptions);
            RepoInfo repo = manifestInfo.AllRepos.First();

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();

            // Execute generator
            string result = McrTagsMetadataGenerator.Execute(
                manifestInfo,
                repo,
                generateGitHubLinks: true,
                gitService: gitServiceMock.Object,
                sourceRepoUrl: SourceRepoUrl,
                sourceBranch: SourceBranch);

            TagsMetadata tagsMetadata = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<TagsMetadata>(result);

            // Verify the output only contains the platform with the documented tag
            Assert.Single(tagsMetadata.Repos[0].TagGroups);
            Assert.Equal(
                $"{SourceRepoUrl}/blob/{SourceBranch}/1.0/{RepoName}/os2/Dockerfile",
                tagsMetadata.Repos[0].TagGroups[0].Dockerfile);

            List<string> expectedTags = new List<string>
            {
                "tag1a",
                "tag1b"
            };
            if (hasSharedTag)
            {
                expectedTags.Add("shared");
            }

            Assert.Equal(expectedTags, tagsMetadata.Repos[0].TagGroups[0].Tags);
        }

        /// <summary>
        /// Verifies that <see cref="McrTagsMetadataGenerator"/> can be run against a platform that is defined
        /// multiple times in different images within the manifest and have the tags combined into one entry.
        /// </summary>
        [Fact]
        public void DuplicatedPlatform()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            const string SourceRepoUrl = "https://www.github.com/dotnet/dotnet-docker";
            const string RepoName = "repo";
            const string SourceBranch = "branch";

            // Create MCR tags metadata template file
            StringBuilder tagsMetadataTemplateBuilder = new StringBuilder();
            tagsMetadataTemplateBuilder.AppendLine($"$(McrTagsYmlRepo:{RepoName})");
            tagsMetadataTemplateBuilder.AppendLine($"$(McrTagsYmlTagGroup:concreteTagA)");
            string tagsMetadataTemplatePath = Path.Combine(tempFolderContext.Path, "tags.yaml");
            File.WriteAllText(tagsMetadataTemplatePath, tagsMetadataTemplateBuilder.ToString());

            string emptyFileName = "emptyFile.md";
            string emptyFilePath = Path.Combine(tempFolderContext.Path, emptyFileName);
            File.WriteAllText(emptyFilePath, string.Empty);

            // Create manifest
            Manifest manifest = ManifestHelper.CreateManifest(
                ManifestHelper.CreateRepo(RepoName,
                    new Image[]
                    {
                        ManifestHelper.CreateImage(
                            new Platform[]
                            {
                                ManifestHelper.CreatePlatform(
                                    DockerfileHelper.CreateDockerfile($"1.0/{RepoName}/os", tempFolderContext),
                                    new string[] { "concreteTagZ", "concreteTagA" })
                            },
                            sharedTags: new Dictionary<string, Tag>
                            {
                                { "shared1", new Tag() },
                                { "latest", new Tag() },
                            }),
                        ManifestHelper.CreateImage(
                            new Platform[]
                            {
                                ManifestHelper.CreatePlatform(
                                    DockerfileHelper.CreateDockerfile($"1.0/{RepoName}/os", tempFolderContext),
                                    Array.Empty<string>())
                            },
                            sharedTags: new Dictionary<string, Tag>
                            {
                                { "shared2", new Tag() }
                            })
                    },
                    readme: emptyFileName,
                    readmeTemplate: emptyFileName,
                    mcrTagsMetadataTemplate: Path.GetFileName(tagsMetadataTemplatePath))
            );

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            // Load manifest
            IManifestOptionsInfo manifestOptions = ManifestHelper.GetManifestOptions(manifestPath);
            ManifestInfo manifestInfo = ManifestInfo.Load(manifestOptions);
            RepoInfo repo = manifestInfo.AllRepos.First();

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();

            // Execute generator
            string result = McrTagsMetadataGenerator.Execute(
                manifestInfo,
                repo,
                generateGitHubLinks: true,
                gitService: gitServiceMock.Object,
                sourceRepoUrl: SourceRepoUrl,
                sourceBranch: SourceBranch);


            TagsMetadata tagsMetadata = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<TagsMetadata>(result);

            // Verify the output only contains the platform with the documented tag
            Assert.Single(tagsMetadata.Repos[0].TagGroups);
            Assert.Equal(
                $"{SourceRepoUrl}/blob/{SourceBranch}/1.0/{RepoName}/os/Dockerfile",
                tagsMetadata.Repos[0].TagGroups[0].Dockerfile);

            List<string> expectedTags = new List<string>
            {
                "concreteTagZ",
                "concreteTagA",
                "shared2",
                "shared1",
                "latest"
            };

            Assert.Equal(expectedTags, tagsMetadata.Repos[0].TagGroups[0].Tags);
        }
    }
}
