// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class BuildCommandTests
    {
        private static Mock<IDockerService> CreateDockerServiceMock(string buildOutput = null)
        {
            Mock<IDockerService> dockerServiceMock = new Mock<IDockerService>();
            dockerServiceMock
                .SetupGet(o => o.Architecture)
                .Returns(Architecture.AMD64);

            dockerServiceMock
                .Setup(o =>
                    o.BuildImage(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<IEnumerable<string>>(),
                        It.IsAny<IDictionary<string, string>>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()))
                .Returns(buildOutput ?? String.Empty);

            return dockerServiceMock;
        }

        /// <summary>
        /// Verifies the command outputs an image info correctly for a basic scenario.
        /// </summary>
        [Fact]
        public async Task BuildCommand_ImageInfoOutput_Basic()
        {
            const string repoName = "runtime";
            const string sha = "sha256:c74364a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd638c";
            string digest = $"{repoName}@{sha}";
            const string tag = "tag";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";
            string baseImageDigest = $"{baseImageRepo}@sha256:d21234a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd1349";

            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

                dockerServiceMock
                    .Setup(o => o.GetImageDigest($"{repoName}:{tag}", false))
                    .Returns(digest);

                dockerServiceMock
                    .Setup(o => o.GetImageDigest(baseImageTag, false))
                    .Returns(baseImageDigest);

                DateTime createdDate = DateTime.Now;

                dockerServiceMock
                    .Setup(o => o.GetCreatedDate($"{repoName}:{tag}", false))
                    .Returns(createdDate);
                
                const string runtimeRelativeDir = "1.0/runtime/os";
                Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRelativePath = PathHelper.NormalizePath(Path.Combine(runtimeRelativeDir, "Dockerfile"));
                string fullDockerfilePath = PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, dockerfileRelativePath));
                File.WriteAllText(fullDockerfilePath, $"FROM {baseImageTag}");

                const string dockerfileCommitSha = "mycommit";
                Mock<IGitService> gitServiceMock = new Mock<IGitService>();
                gitServiceMock
                    .Setup(o => o.GetCommitSha(fullDockerfilePath, It.IsAny<bool>()))
                    .Returns(dockerfileCommitSha);

                BuildCommand command = new BuildCommand(dockerServiceMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IEnvironmentService>(),
                    gitServiceMock.Object);
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "image-info.json");
                command.Options.IsPushEnabled = true;
                command.Options.SourceRepoUrl = "https://github.com/dotnet/test";

                const string ProductVersion = "1.0.1";

                Manifest manifest = CreateManifest(
                    CreateRepo(repoName,
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(dockerfileRelativePath, new string[] { tag })
                            },
                            new Dictionary<string, Tag>
                            {
                                { "shared", new Tag() }
                            },
                            ProductVersion))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
                {
                    Repos =
                    {
                        new RepoData
                        {
                            Repo = repoName,
                            Images =
                            {
                                new ImageData
                                {
                                    ProductVersion = ProductVersion,
                                    Platforms =
                                    {
                                        new PlatformData
                                        {
                                            Dockerfile = $"{runtimeRelativeDir}/Dockerfile",
                                            Architecture = "amd64",
                                            OsType = "Linux",
                                            OsVersion = "Ubuntu 19.04",
                                            Digest = sha,
                                            BaseImageDigest = baseImageDigest,
                                            Created = createdDate.ToUniversalTime(),
                                            SimpleTags =
                                            {
                                                tag
                                            },
                                            CommitUrl = $"{command.Options.SourceRepoUrl}/blob/{dockerfileCommitSha}/{dockerfileRelativePath}"
                                        }
                                    },
                                    Manifest = new ManifestData
                                    {
                                        SharedTags =
                                        {
                                            "shared"
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                string expectedOutput = JsonHelper.SerializeObject(imageArtifactDetails);
                string actualOutput = File.ReadAllText(command.Options.ImageInfoOutputPath);

                Assert.Equal(expectedOutput, actualOutput);
            }
        }

        /// <summary>
        /// Verifies the tags that get built and published.
        /// </summary>
        [Fact]
        public async Task BuildCommand_Publish()
        {
            const string repoName = "runtime";
            const string tag = "tag";
            const string sharedTag = "shared";
            const string baseImageRepo = "baserepo";
            string baseImageTag = $"{baseImageRepo}:basetag";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();

            BuildCommand command = new BuildCommand(dockerServiceMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IEnvironmentService>(),
                Mock.Of<IGitService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.IsPushEnabled = true;

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile");
            string dockerfileAbsolutePath = PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, dockerfileRelativePath));
            File.WriteAllText(dockerfileAbsolutePath, $"FROM {baseImageTag}");

            Manifest manifest = CreateManifest(
                CreateRepo(repoName,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRelativePath, new string[] { tag })
                        },
                        new Dictionary<string, Tag>
                        {
                            { sharedTag, new Tag() }
                        }))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            dockerServiceMock.Verify(
                o => o.BuildImage(
                    dockerfileAbsolutePath,
                    PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, runtimeRelativeDir)),
                    new string[]
                    {
                        TagInfo.GetFullyQualifiedName(repoName, tag),
                        TagInfo.GetFullyQualifiedName(repoName, sharedTag)
                    },
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()));

            dockerServiceMock.Verify(
                o => o.PushImage(TagInfo.GetFullyQualifiedName(repoName, tag), It.IsAny<bool>()));
            dockerServiceMock.Verify(
                o => o.PushImage(TagInfo.GetFullyQualifiedName(repoName, sharedTag), It.IsAny<bool>()));
        }

        /// <summary>
        /// Verifies the command outputs an image info correctly when the manifest references a custom named Dockerfile.
        /// </summary>
        [Fact]
        public async Task BuildCommand_ImageInfoOutput_CustomDockerfile()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock();
                const string digest = "runtime@sha256:c74364a9f125ca612f9a67e4a0551937b7a37c82fabb46172c4867b73edd638c";
                dockerServiceMock
                    .Setup(o => o.GetImageDigest("runtime:runtime", false))
                    .Returns(digest);

                const string runtimeRelativeDir = "1.0/runtime/os";
                Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile.custom");
                File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

                const string dockerfileCommitSha = "mycommit";
                Mock<IGitService> gitServiceMock = new Mock<IGitService>();
                gitServiceMock
                    .Setup(o => o.GetCommitSha(dockerfileRelativePath, It.IsAny<bool>()))
                    .Returns(dockerfileCommitSha);

                BuildCommand command = new BuildCommand(dockerServiceMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IEnvironmentService>(),
                    gitServiceMock.Object);
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.ImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "image-info.json");
                command.Options.SourceRepoUrl = "https://source";

                Manifest manifest = CreateManifest(
                    CreateRepo("runtime",
                        CreateImage(
                            CreatePlatform(dockerfileRelativePath, new string[] { "runtime" })))
                );

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                ImageArtifactDetails imageArtifactDetails = JsonConvert.DeserializeObject<ImageArtifactDetails>(
                    File.ReadAllText(command.Options.ImageInfoOutputPath));
                Assert.Equal(
                    PathHelper.NormalizePath(dockerfileRelativePath),
                    imageArtifactDetails.Repos[0].Images.First().Platforms.First().Dockerfile);
            }
        }

        /// <summary>
        /// Verifies an exception is thrown if the build output contains pull information.
        /// </summary>
        [Fact]
        public async Task BuildCommand_ThrowsIfImageIsPulled()
        {
            Mock<IDockerService> dockerServiceMock = CreateDockerServiceMock("Pulling from");

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            
            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = PathHelper.NormalizePath(Path.Combine(runtimeRelativeDir, "Dockerfile"));
            string fullDockerfilePath = PathHelper.NormalizePath(Path.Combine(tempFolderContext.Path, dockerfileRelativePath));
            File.WriteAllText(fullDockerfilePath, $"FROM baserepo:basetag");

            BuildCommand command = new BuildCommand(dockerServiceMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IEnvironmentService>(),
                Mock.Of<IGitService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                                CreatePlatform(dockerfileRelativePath, new string[] { "tag" })
                        },
                        new Dictionary<string, Tag>
                        {
                                { "shared", new Tag() }
                        },
                        "1.0.1"))
            );

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();

            await Assert.ThrowsAsync<InvalidOperationException>(command.ExecuteAsync);
        }
    }
}
