#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ImageBuilder.Mcr;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.McrTags;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

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

        public class MultiPlatformTags : IDisposable
        {
            private const string EmptyFileName = "emptyFile.md";
            private const string TagsMetadataTemplateName = "tags.yml";
            private const string ManifestFileName = "manifest.json";
            private const string RepoName = "repo";
            private const string SharedTagName = "sharedTag";
            private const string LinuxOsName = "noble";
            private const string WindowsOsName = "nanoserver-ltsc2025";

            private readonly TempFolderContext _tempFolderContext = TestHelper.UseTempFolder();

            public MultiPlatformTags()
            {
                File.WriteAllText(EmptyFilePath, string.Empty);
            }

            private string EmptyFilePath =>
                Path.Combine(_tempFolderContext.Path, EmptyFileName);

            private string TagsMetadataTemplateFilePath =>
                Path.Combine(_tempFolderContext.Path, TagsMetadataTemplateName);

            private string ManifestFilePath =>
                Path.Combine(_tempFolderContext.Path, ManifestFileName);

            private IEnumerable<Platform> CreatePlatforms(
                IEnumerable<string> tags,
                IEnumerable<Architecture> architectures,
                TagDocumentationType tagDocumentationType,
                OS os)
            {
                string osVersion = os == OS.Windows ? WindowsOsName : LinuxOsName;
                return architectures.Select(arch =>
                    CreatePlatform(
                        DockerfileHelper.CreateDockerfile($"1.0/{RepoName}/{osVersion}/{arch}", _tempFolderContext),
                        GetTags(tags, osVersion, arch),
                        os: os,
                        osVersion: osVersion,
                        architecture: arch,
                        tagDocumentationType: tagDocumentationType));
            }

            private static string[] GetTags(IEnumerable<string> tags, string osVersion, Architecture arch) =>
                tags.Select(tag => $"{tag}-{osVersion}-{arch.ToString().ToLowerInvariant()}").ToArray();

            [Fact]
            public void DocumentedPlatformTags()
            {
                const string tagsMetadataTemplate = $"""
                $(McrTagsYmlRepo:{RepoName})
                $(McrTagsYmlTagGroup:{SharedTagName})
                """;
                File.WriteAllText(TagsMetadataTemplateFilePath, tagsMetadataTemplate);

                IEnumerable<Architecture> architectures = [Architecture.AMD64, Architecture.ARM64, Architecture.ARM];
                IEnumerable<string> tags = ["tag1", "tag2"];
                TagDocumentationType tagDocumentationType = TagDocumentationType.Documented;
                IEnumerable<Platform> platforms = CreatePlatforms(tags, architectures, tagDocumentationType, OS.Linux);

                ValidateMetadataGeneratorOutput(platforms);
            }

            [Fact]
            public void UndocumentedPlatformTags()
            {
                const string tagsMetadataTemplate = $"""
                $(McrTagsYmlRepo:{RepoName})
                $(McrTagsYmlTagGroup:{SharedTagName})
                """;
                File.WriteAllText(TagsMetadataTemplateFilePath, tagsMetadataTemplate);

                IEnumerable<Architecture> architectures = [Architecture.AMD64, Architecture.ARM64, Architecture.ARM];
                IEnumerable<string> tags = ["tag1", "tag2"];
                TagDocumentationType tagDocumentationType = TagDocumentationType.Undocumented;
                IEnumerable<Platform> platforms = CreatePlatforms(tags, architectures, tagDocumentationType, OS.Linux);

                ValidateMetadataGeneratorOutput(platforms);
            }

            [Fact]
            public void NoPlatformTags()
            {
                const string tagsMetadataTemplate = $"""
                $(McrTagsYmlRepo:{RepoName})
                $(McrTagsYmlTagGroup:{SharedTagName})
                """;
                File.WriteAllText(TagsMetadataTemplateFilePath, tagsMetadataTemplate);

                IEnumerable<Architecture> architectures = [Architecture.AMD64, Architecture.ARM64, Architecture.ARM];
                IEnumerable<string> tags = [];
                TagDocumentationType tagDocumentationType = TagDocumentationType.Documented;
                IEnumerable<Platform> platforms = CreatePlatforms(tags, architectures, tagDocumentationType, OS.Linux);

                ValidateMetadataGeneratorOutput(platforms);
            }

            [Fact]
            public void SharedLinuxAndWindowsTags()
            {
                // Windows images should be excluded from computation of multi-arch tags
                const string tagsMetadataTemplate = $"""
                $(McrTagsYmlRepo:{RepoName})
                $(McrTagsYmlTagGroup:{SharedTagName})
                $(McrTagsYmlTagGroup:tag1-{WindowsOsName}-amd64)
                """;
                File.WriteAllText(TagsMetadataTemplateFilePath, tagsMetadataTemplate);

                IEnumerable<string> tags = ["tag1", "tag2"];
                TagDocumentationType tagDocumentationType = TagDocumentationType.Documented;

                IEnumerable<Platform> platforms =
                [
                    ..CreatePlatforms(
                        tags,
                        [Architecture.AMD64, Architecture.ARM64, Architecture.ARM],
                        tagDocumentationType,
                        OS.Linux),
                    ..CreatePlatforms(
                        tags,
                        [Architecture.AMD64],
                        tagDocumentationType,
                        OS.Windows)
                ];

                ValidateMetadataGeneratorOutput(platforms);
            }

            private void ValidateMetadataGeneratorOutput(IEnumerable<Platform> platforms)
            {
                // Create Manifest with a single image and the provided platforms
                Manifest manifest =
                    CreateManifest(
                        CreateRepo(
                            name: RepoName,
                            images:
                            [
                                CreateImage(
                                    platforms: platforms,
                                    sharedTags: new Dictionary<string, Tag>() { [SharedTagName] = new Tag() })
                            ],
                            mcrTagsMetadataTemplate: Path.GetFileName(TagsMetadataTemplateFilePath),
                            readme: EmptyFileName,
                            readmeTemplate: EmptyFileName));

                File.WriteAllText(ManifestFilePath, JsonConvert.SerializeObject(manifest));

                // Load manifest
                IManifestOptionsInfo manifestOptions = GetManifestOptions(ManifestFilePath);
                ManifestInfo manifestInfo = ManifestInfo.Load(manifestOptions);
                RepoInfo repo = manifestInfo.AllRepos.First();

                // Execute tags metadata generator and deserialize its output
                string result = McrTagsMetadataGenerator.Execute(manifestInfo, repo);
                TagsMetadata tagsMetadata = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build()
                    .Deserialize<TagsMetadata>(result);

                // Now, check the output of the metadata generator against the tags that were passed in via `platforms`.
                // The shared tag should always show up.
                // Get the platform-specific tags from the platforms that were passed in.
                IEnumerable<IEnumerable<string>> expectedTags = platforms
                    .Select(platform => platform.Tags
                        .Where(tag => tag.Value.DocType != TagDocumentationType.Undocumented)
                        .Select(tag => tag.Key)
                        .Concat([SharedTagName]));

                IEnumerable<IEnumerable<string>> actualTags =
                    tagsMetadata.Repos[0].TagGroups.Select(group => group.Tags);

                expectedTags.Should().BeEquivalentTo(actualTags);
            }

            public void Dispose()
            {
                _tempFolderContext.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}
