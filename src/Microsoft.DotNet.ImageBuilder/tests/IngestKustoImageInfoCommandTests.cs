// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class IngestKustoImageInfoCommandTests
    {
        private ITestOutputHelper _outputHelper;

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
            string repo2Image2DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/sdk/os", tempFolderContext);
            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        CreatePlatform(repo1Image1DockerfilePath, new string[0]))),
                CreateRepo("repo2",
                    CreateImage(
                        CreatePlatform(repo2Image2DockerfilePath, new string[0])))
            );
            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            ImageArtifactDetails srcImageArtifactDetails = new ImageArtifactDetails
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
                                Manifest =
                                new ManifestData
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
                                            })
                                    },
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(
                                            repo1Image1DockerfilePath,
                                            created: new DateTime(2020, 4, 20, 21, 56, 56, DateTimeKind.Utc),
                                            digest: "ghi",
                                            simpleTags: new List<string>
                                            {
                                                "t2"
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
                                        })
                                },
                                ProductVersion = "2.0.5"
                            }
                        }
                    }
                }
            };

            string expectedData =
@"""def"",""amd64"",""Linux"",""Ubuntu 19.04"",""1.0.2"",""1.0/sdk/os/Dockerfile"",""r1"",""2020-04-20 21:56:50""
""t1"",""amd64"",""Linux"",""Ubuntu 19.04"",""1.0.2"",""1.0/sdk/os/Dockerfile"",""r1"",""2020-04-20 21:56:50""
""ghi"",""amd64"",""Linux"",""Ubuntu 19.04"",""1.0.2"",""1.0/sdk/os/Dockerfile"",""r1"",""2020-04-20 21:56:56""
""t2"",""amd64"",""Linux"",""Ubuntu 19.04"",""1.0.2"",""1.0/sdk/os/Dockerfile"",""r1"",""2020-04-20 21:56:56""
""jkl"",""amd64"",""Linux"",""Ubuntu 19.04"",""2.0.5"",""2.0/sdk/os/Dockerfile"",""r2"",""2020-04-20 21:56:58""
""t3"",""amd64"",""Linux"",""Ubuntu 19.04"",""2.0.5"",""2.0/sdk/os/Dockerfile"",""r2"",""2020-04-20 21:56:58""";
            expectedData = expectedData.NormalizeLineEndings(Environment.NewLine).Trim();

            string imageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            File.WriteAllText(imageInfoPath, JsonHelper.SerializeObject(srcImageArtifactDetails));

            string ingestedData = null;

            Mock<IKustoClient> kustoClientMock = new Mock<IKustoClient>();
            kustoClientMock
                .Setup(o => o.IngestFromCsvStreamAsync(It.IsAny<Stream>(), It.IsAny<IngestKustoImageInfoOptions>()))
                .Callback<Stream, IngestKustoImageInfoOptions>((s, o) =>
                {
                    StreamReader reader = new StreamReader(s);
                    ingestedData = reader.ReadToEnd();
                });
            IngestKustoImageInfoCommand command = new IngestKustoImageInfoCommand(
                Mock.Of<ILoggerService>(), kustoClientMock.Object);
            command.Options.ImageInfoPath = imageInfoPath;
            command.Options.Manifest = manifestPath;

            command.LoadManifest();
            await command.ExecuteAsync();

            _outputHelper.WriteLine($"Expected Data: {Environment.NewLine}{expectedData}");
            _outputHelper.WriteLine($"Actual Data: {Environment.NewLine}{ingestedData}");

            kustoClientMock.Verify(o => o.IngestFromCsvStreamAsync(It.IsAny<Stream>(), It.IsAny<IngestKustoImageInfoOptions>()));
            Assert.Equal(expectedData, ingestedData);
        }
    }
}
