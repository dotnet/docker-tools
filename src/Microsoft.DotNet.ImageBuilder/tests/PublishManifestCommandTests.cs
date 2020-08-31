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
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class PublishManifestCommandTests
    {
        /// <summary>
        /// Verifies the image info output for shared tags.
        /// </summary>
        [Fact]
        public async Task ImageInfoTagOutput()
        {
            Mock<IManifestToolService> manifestToolService = new Mock<IManifestToolService>();
            manifestToolService
                .Setup(o => o.Inspect("repo1:sharedtag2", false))
                .Returns(ManifestToolServiceHelper.CreateTagManifest(ManifestToolService.ManifestListMediaType, "digest1"));
            manifestToolService
                .Setup(o => o.Inspect("repo2:sharedtag3", false))
                .Returns(ManifestToolServiceHelper.CreateTagManifest(ManifestToolService.ManifestListMediaType, "digest2"));

            PublishManifestCommand command = new PublishManifestCommand(manifestToolService.Object,
                Mock.Of<IEnvironmentService>(), Mock.Of<ILoggerService>());

            using TempFolderContext tempFolderContext = new TempFolderContext();

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfile1 = CreateDockerfile("1.0/repo1/os", tempFolderContext);
            string dockerfile2 = CreateDockerfile("1.0/repo2/os", tempFolderContext);
            string dockerfile3 = CreateDockerfile("1.0/repo3/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
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
                                    CreatePlatform(dockerfile1),
                                    new PlatformData
                                    {
                                        
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag1",
                                        "sharedtag2"
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo2",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(dockerfile2),
                                    new PlatformData
                                    {
                                        IsCached = true
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag1",
                                        "sharedtag3"
                                    }
                                }
                            }
                        }
                    }
                }
            };
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1, new string[] { "tag1" })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "sharedtag2", new Tag() },
                            { "sharedtag1", new Tag() }
                        })),
                CreateRepo("repo2",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile2, new string[] { "tag2" })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "sharedtag3", new Tag() },
                            { "sharedtag1", new Tag() }
                        }))
            );
            File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            string actualOutput = File.ReadAllText(command.Options.ImageInfoPath);

            ImageArtifactDetails actualImageArtifactDetails = JsonConvert.DeserializeObject<ImageArtifactDetails>(actualOutput);

            // Since we don't know what the exact Created time will be that the command has calculated, we're going to
            // pull it from the data, verify that it's recent and then use it for constructing our expected data value.
            DateTime actualCreatedDate = actualImageArtifactDetails.Repos[0].Images[0].Manifest.Created;
            Assert.True(actualCreatedDate > (DateTime.Now.ToUniversalTime() - TimeSpan.FromMinutes(1)));
            Assert.True(actualCreatedDate < (DateTime.Now.ToUniversalTime() + TimeSpan.FromMinutes(1)));

            imageArtifactDetails.Repos[0].Images[0].Manifest.Digest = "repo1@digest1";
            imageArtifactDetails.Repos[0].Images[0].Manifest.Created = actualCreatedDate;
            imageArtifactDetails.Repos[1].Images[0].Manifest.Digest = "repo2@digest2";
            imageArtifactDetails.Repos[1].Images[0].Manifest.Created = actualCreatedDate;

            // Remove cached platform
            imageArtifactDetails.Repos[1].Images[0].Platforms.RemoveAt(1);

            string expectedOutput = JsonHelper.SerializeObject(imageArtifactDetails);

            Assert.Equal(expectedOutput, actualOutput);
        }
    }
}
