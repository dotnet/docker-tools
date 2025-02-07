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
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class IngestKustoImageInfoCommandTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public IngestKustoImageInfoCommandTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        /// <summary>
        /// Verifies the command will ingest multiple repos.
        /// </summary>
        [Fact]
        public async Task IngestKustoImageInfoCommand_MultipleRepos()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/sdk/os", tempFolderContext);
            string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/sdk/os2", tempFolderContext);
            string repo2Image2DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/sdk/os", tempFolderContext);
            Manifest manifest = CreateManifest(
                CreateRepo("r1",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(repo1Image1DockerfilePath, new string[] { "t1" }),
                            CreatePlatform(repo1Image2DockerfilePath, new string[] { "t2" })
                        },
                        productVersion: "1.0.2",
                        sharedTags: new Dictionary<string, Tag>
                        {
                            { "st1", new Tag() }
                        })),
                CreateRepo("r2",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(repo2Image2DockerfilePath, new string[] { "t3" })
                        },
                        productVersion: "2.0.5"))
            );
            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            ImageArtifactDetails srcImageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "r1",
                        Images =
                        {
                            new ImageData
                            {
                                Manifest = new ManifestData
                                {
                                    Created = new DateTime(2020, 4, 20, 21, 57, 00, DateTimeKind.Utc),
                                    Digest = "abc",
                                    SharedTags = new List<string>
                                    {
                                        "st1"
                                    }
                                },
                                Platforms =
                                {
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(
                                            repo1Image1DockerfilePath,
                                            created: new DateTime(2020, 4, 20, 21, 56, 50, DateTimeKind.Utc),
                                            digest: "def",
                                            simpleTags: new List<string>
                                            {
                                                "t1"
                                            },
                                            layers: new List<string>
                                            {
                                                "qwe",
                                                "asd"
                                            })
                                    },
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(
                                            repo1Image2DockerfilePath,
                                            created: new DateTime(2020, 4, 20, 21, 56, 56, DateTimeKind.Utc),
                                            digest: "ghi",
                                            simpleTags: new List<string>
                                            {
                                                "t2"
                                            },
                                            layers: new List<string>
                                            {
                                                "qwe"
                                            })
                                    }
                                },
                                ProductVersion = "1.0.2",
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "r2",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(
                                        repo2Image2DockerfilePath,
                                        created: new DateTime(2020, 4, 20, 21, 56, 58, DateTimeKind.Utc),
                                        digest: "jkl",
                                        simpleTags: new List<string>
                                        {
                                            "t3"
                                        },
                                        layers: new List<string>
                                        {
                                            "zxc"
                                        })
                                },
                                ProductVersion = "2.0.5"
                            }
                        }
                    }
                }
            };

            string expectedImageData =
@"""def"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.2"",""1.0/sdk/os/Dockerfile"",""r1"",""2020-04-20 21:56:50""
""t1"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.2"",""1.0/sdk/os/Dockerfile"",""r1"",""2020-04-20 21:56:50""
""ghi"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.2"",""1.0/sdk/os2/Dockerfile"",""r1"",""2020-04-20 21:56:56""
""t2"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.2"",""1.0/sdk/os2/Dockerfile"",""r1"",""2020-04-20 21:56:56""
""jkl"",""amd64"",""Linux"",""Ubuntu 24.04"",""2.0.5"",""2.0/sdk/os/Dockerfile"",""r2"",""2020-04-20 21:56:58""
""t3"",""amd64"",""Linux"",""Ubuntu 24.04"",""2.0.5"",""2.0/sdk/os/Dockerfile"",""r2"",""2020-04-20 21:56:58""";
            expectedImageData = expectedImageData.NormalizeLineEndings(Environment.NewLine).Trim();

            string expectedLayerData =
@"""qwe"",""0"",""2"",""def"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.2"",""1.0/sdk/os/Dockerfile"",""r1"",""2020-04-20 21:56:50""
""asd"",""0"",""1"",""def"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.2"",""1.0/sdk/os/Dockerfile"",""r1"",""2020-04-20 21:56:50""
""qwe"",""0"",""1"",""ghi"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.2"",""1.0/sdk/os2/Dockerfile"",""r1"",""2020-04-20 21:56:56""
""zxc"",""0"",""1"",""jkl"",""amd64"",""Linux"",""Ubuntu 24.04"",""2.0.5"",""2.0/sdk/os/Dockerfile"",""r2"",""2020-04-20 21:56:58""";
            expectedLayerData = expectedLayerData.NormalizeLineEndings(Environment.NewLine).Trim();

            await ValidateExecuteAsync(tempFolderContext, manifestPath, srcImageArtifactDetails, expectedImageData, expectedLayerData);
        }

        /// <summary>
        /// Verifies the command will ingest syndicated tags to another repo.
        /// </summary>
        [Fact]
        public async Task SyndicatedTag()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/sdk/os", tempFolderContext);
            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(repo1Image1DockerfilePath, new string[] { "t1", "t2", "t3" } )
                        },
                        productVersion: "1.0.5"))
            );

            const string syndicatedRepository = "repo2";
            Platform platform = manifest.Repos.First().Images.First().Platforms.First();
            platform.Tags["t1"].Syndication = new TagSyndication
            {
                Repo = syndicatedRepository
            };
            platform.Tags["t2"].Syndication = new TagSyndication
            {
                Repo = syndicatedRepository,
                DestinationTags = new string[]
                {
                    "t2a",
                    "t2b"
                }
            };

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            ImageArtifactDetails srcImageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(
                                        repo1Image1DockerfilePath,
                                        created: new DateTime(2020, 4, 20, 21, 56, 58, DateTimeKind.Utc),
                                        digest: "jkl",
                                        simpleTags: new List<string>
                                        {
                                            "t1",
                                            "t2",
                                            "t3"
                                        },
                                        layers: new List<string>
                                        {
                                            "zxc"
                                        })
                                },
                                ProductVersion = "1.0.5"
                            }
                        }
                    }
                }
            };

            string expectedImageData =
@"""jkl"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.5"",""1.0/sdk/os/Dockerfile"",""repo1"",""2020-04-20 21:56:58""
""jkl"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.5"",""1.0/sdk/os/Dockerfile"",""repo2"",""2020-04-20 21:56:58""
""t1"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.5"",""1.0/sdk/os/Dockerfile"",""repo1"",""2020-04-20 21:56:58""
""t1"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.5"",""1.0/sdk/os/Dockerfile"",""repo2"",""2020-04-20 21:56:58""
""t2"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.5"",""1.0/sdk/os/Dockerfile"",""repo1"",""2020-04-20 21:56:58""
""t2a"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.5"",""1.0/sdk/os/Dockerfile"",""repo2"",""2020-04-20 21:56:58""
""t2b"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.5"",""1.0/sdk/os/Dockerfile"",""repo2"",""2020-04-20 21:56:58""
""t3"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.5"",""1.0/sdk/os/Dockerfile"",""repo1"",""2020-04-20 21:56:58""";
            expectedImageData = expectedImageData.NormalizeLineEndings(Environment.NewLine).Trim();

            string expectedLayerData =
@"""zxc"",""0"",""1"",""jkl"",""amd64"",""Linux"",""Ubuntu 24.04"",""1.0.5"",""1.0/sdk/os/Dockerfile"",""repo1"",""2020-04-20 21:56:58""";
            expectedLayerData = expectedLayerData.NormalizeLineEndings(Environment.NewLine).Trim();

            await ValidateExecuteAsync(tempFolderContext, manifestPath, srcImageArtifactDetails, expectedImageData, expectedLayerData);
        }

        private async Task ValidateExecuteAsync(
            TempFolderContext tempFolderContext,
            string manifestPath,
            ImageArtifactDetails srcImageArtifactDetails,
            string expectedImageData,
            string expectedLayerData)
        {
            string imageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            File.WriteAllText(imageInfoPath, JsonHelper.SerializeObject(srcImageArtifactDetails));

            Dictionary<string, string> ingestedData = new();

            Mock<IKustoClient> kustoClientMock = new();
            kustoClientMock
                .Setup(o => o.IngestFromCsvAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<string, string, string, string, bool>(
                    (csv, cluster, database, table, isDryRun) =>
                    {
                        ingestedData.Add(table, csv);
                    });
            IngestKustoImageInfoCommand command = new(Mock.Of<ILoggerService>(), kustoClientMock.Object);
            command.Options.ImageInfoPath = imageInfoPath;
            command.Options.Manifest = manifestPath;
            command.Options.ImageTable = "ImageInfo";
            command.Options.LayerTable = "LayerInfo";

            command.LoadManifest();
            await command.ExecuteAsync();

            _outputHelper.WriteLine($"Expected Image Data: {Environment.NewLine}{expectedImageData}");
            _outputHelper.WriteLine($"Actual Image Data: {Environment.NewLine}{ingestedData[command.Options.ImageTable]}");

            _outputHelper.WriteLine($"Expected Layer Data: {Environment.NewLine}{expectedLayerData}");
            _outputHelper.WriteLine($"Actual Layer Data: {Environment.NewLine}{ingestedData[command.Options.LayerTable]}");

            kustoClientMock.Verify(o => o.IngestFromCsvAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
            Assert.Equal(expectedImageData, ingestedData[command.Options.ImageTable]);
            Assert.Equal(expectedLayerData, ingestedData[command.Options.LayerTable]);
        }
    }
}
