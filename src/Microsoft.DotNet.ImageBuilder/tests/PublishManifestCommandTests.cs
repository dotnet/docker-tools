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
                .Setup(o => o.InspectAsync("repo1:sharedtag2", false))
                .ReturnsAsync(ManifestToolServiceHelper.CreateTagManifest(ManifestToolService.ManifestListMediaType, "digest1"));
            manifestToolService
                .Setup(o => o.InspectAsync("repo2:sharedtag3", false))
                .ReturnsAsync(ManifestToolServiceHelper.CreateTagManifest(ManifestToolService.ManifestListMediaType, "digest2"));

            DateTime manifestCreatedDate = DateTime.UtcNow;
            IDateTimeService dateTimeService = Mock.Of<IDateTimeService>(o => o.UtcNow == manifestCreatedDate);

            PublishManifestCommand command = new PublishManifestCommand(
                manifestToolService.Object, Mock.Of<ILoggerService>(), dateTimeService);

            using TempFolderContext tempFolderContext = new TempFolderContext();

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfile1 = CreateDockerfile("1.0/repo1/os", tempFolderContext);
            string dockerfile2 = CreateDockerfile("1.0/repo2/os", tempFolderContext);
            string dockerfile3 = CreateDockerfile("1.0/repo3/os", tempFolderContext);

            const string digest1 = "sha256:123";
            const string digest2 = "sha256:ABC";
            const string digest3 = "sha256:DEF";

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
                                    CreatePlatform(dockerfile1, digest: digest1, simpleTags: new List<string>{ "tag1" }),
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
                                    CreatePlatform(dockerfile2, digest: digest2, simpleTags: new List<string>{ "tag2" })
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
                    },
                    new RepoData
                    {
                        Repo = "repo3",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(dockerfile3, digest: digest3, simpleTags: new List<string>{ "tag3" })
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
                        })),
                CreateRepo("repo3",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile3, new string[] { "tag3" })
                        })),
                CreateRepo("unpublishedrepo",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                CreateDockerfile("1.0/unpublishedrepo/os", tempFolderContext),
                                new string[] { "tag" })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "sharedtag", new Tag() }
                        }))
            );
            File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            string actualOutput = File.ReadAllText(command.Options.ImageInfoPath);

            ImageArtifactDetails actualImageArtifactDetails = JsonConvert.DeserializeObject<ImageArtifactDetails>(actualOutput);

            ImageArtifactDetails expectedImageArtifactDetails = new()
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
                                    new PlatformData
                                    {
                                        Dockerfile = "1.0/repo1/os/Dockerfile",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Architecture = "amd64",
                                        Digest = digest1,
                                        SimpleTags = new List<string> { "tag1" }
                                    },
                                    new PlatformData()
                                },
                                Manifest = new ManifestData
                                {
                                    Created = manifestCreatedDate,
                                    Digest = "repo1@digest1",
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
                                    new PlatformData
                                    {
                                        Dockerfile = "1.0/repo2/os/Dockerfile",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Architecture = "amd64",
                                        Digest = digest2,
                                        SimpleTags = new List<string> { "tag2" }
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    Created = manifestCreatedDate,
                                    Digest = "repo2@digest2",
                                    SharedTags =
                                    {
                                        "sharedtag1",
                                        "sharedtag3"
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo3",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "1.0/repo3/os/Dockerfile",
                                        OsType = "Linux",
                                        OsVersion = "focal",
                                        Architecture = "amd64",
                                        Digest = digest3,
                                        SimpleTags = new List<string> { "tag3" }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedImageArtifactDetails);

            Assert.Equal(expectedOutput, actualOutput);
        }

        /// <summary>
        /// Verifies a correct manifest is generated when there are duplicate platforms defined, with some not containing
        /// concrete tags.
        /// </summary>
        [Fact]
        public async Task DuplicatePlatform()
        {
            string expectedManifest1 =
@"image: mcr.microsoft.com/repo1:sharedtag2
tags: [sharedtag1]
manifests:
- image: mcr.microsoft.com/repo1:tag1
  platform:
    architecture: amd64
    os: linux
";

            string expectedManifest2 =
@"image: mcr.microsoft.com/repo1:sharedtag3
manifests:
- image: mcr.microsoft.com/repo1:tag1
  platform:
    architecture: amd64
    os: linux
";

            bool manifest1Found = false;
            bool manifest2Found = false;

            Mock<IManifestToolService> manifestToolService = new Mock<IManifestToolService>();
            manifestToolService
                .Setup(o => o.PushFromSpec(It.IsAny<string>(), false))
                .Callback((string manifestFile, bool isDryRun) =>
                {
                    string manifestContents = File.ReadAllText(manifestFile);

                    if (manifestContents == expectedManifest1)
                    {
                        manifest1Found = true;
                    }
                    else if (manifestContents == expectedManifest2)
                    {
                        manifest2Found = true;
                    }
                });

            manifestToolService
                .Setup(o => o.InspectAsync(It.IsAny<string>(), false))
                .ReturnsAsync(ManifestToolServiceHelper.CreateTagManifest(ManifestToolService.ManifestListMediaType, "digest"));

            PublishManifestCommand command = new PublishManifestCommand(
                manifestToolService.Object, Mock.Of<ILoggerService>(), Mock.Of<IDateTimeService>());

            using TempFolderContext tempFolderContext = new TempFolderContext();

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfile = CreateDockerfile("1.0/repo1/os", tempFolderContext);

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
                                    CreatePlatform(dockerfile,
                                        simpleTags: new List<string>
                                        {
                                            "tag1",
                                            "tag2"
                                        })
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag1",
                                        "sharedtag2"
                                    }
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(dockerfile)
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
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
                            CreatePlatform(dockerfile,
                                new string[]
                                {
                                    "tag1",
                                    "tag2"
                                })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "sharedtag2", new Tag() },
                            { "sharedtag1", new Tag() }
                        }),
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile, Array.Empty<string>())
                        },
                        new Dictionary<string, Tag>
                        {
                            { "sharedtag3", new Tag() }
                        }))
            );
            manifest.Registry = "mcr.microsoft.com";
            File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            Assert.True(manifest1Found);
            Assert.True(manifest2Found);
            manifestToolService
                .Verify(o => o.PushFromSpec(It.IsAny<string>(), false), Times.Exactly(2));
        }

        /// <summary>
        /// Verifies a correct manifest is generated when syndicating tags to another repo.
        /// </summary>
        [Fact]
        public async Task SyndicatedTag()
        {
            string expectedManifest1 =
@"image: mcr.microsoft.com/repo:sharedtag2
tags: [sharedtag1]
manifests:
- image: mcr.microsoft.com/repo:tag1
  platform:
    architecture: amd64
    os: linux
- image: mcr.microsoft.com/repo:tag3
  platform:
    architecture: amd64
    os: linux
";

            string expectedManifest2 =
@"image: mcr.microsoft.com/repo2:sharedtag2a
tags: [sharedtag2b]
manifests:
- image: mcr.microsoft.com/repo2:tag2
  platform:
    architecture: amd64
    os: linux
";

            bool manifest1Found = false;
            bool manifest2Found = false;

            Mock<IManifestToolService> manifestToolService = new Mock<IManifestToolService>();
            manifestToolService
                .Setup(o => o.PushFromSpec(It.IsAny<string>(), false))
                .Callback((string manifestFile, bool isDryRun) =>
                {
                    string manifestContents = File.ReadAllText(manifestFile);

                    if (manifestContents == expectedManifest1)
                    {
                        manifest1Found = true;
                    }
                    else if (manifestContents == expectedManifest2)
                    {
                        manifest2Found = true;
                    }
                });

            manifestToolService
                .Setup(o => o.InspectAsync(It.IsAny<string>(), false))
                .ReturnsAsync(ManifestToolServiceHelper.CreateTagManifest(ManifestToolService.ManifestListMediaType, "digest"));

            DateTime manifestCreatedDate = DateTime.UtcNow;
            IDateTimeService dateTimeService = Mock.Of<IDateTimeService>(o => o.UtcNow == manifestCreatedDate);

            PublishManifestCommand command = new PublishManifestCommand(
                manifestToolService.Object, Mock.Of<ILoggerService>(), dateTimeService);

            using TempFolderContext tempFolderContext = new TempFolderContext();

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfile1 = CreateDockerfile("1.0/repo/os", tempFolderContext);
            string dockerfile2 = CreateDockerfile("1.0/repo/os2", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(dockerfile1,
                                        simpleTags: new List<string>
                                        {
                                            "tag1",
                                            "tag2"
                                        })
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
                    }
                }
            };
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            const string syndicatedRepo2 = "repo2";

            Platform platform1;
            Platform platform2;

            Manifest manifest = CreateManifest(
                CreateRepo("repo",
                    CreateImage(
                        new Platform[]
                        {
                            platform1 = CreatePlatform(dockerfile1, Array.Empty<string>()),
                            platform2 = CreatePlatform(dockerfile2, Array.Empty<string>())
                        },
                        new Dictionary<string, Tag>
                        {
                            {
                                "sharedtag2",
                                new Tag
                                {
                                    Syndication = new TagSyndication
                                    {
                                        Repo = syndicatedRepo2,
                                        DestinationTags = new string[]
                                        {
                                            "sharedtag2a",
                                            "sharedtag2b"
                                        }
                                    }
                                }
                            },
                            { "sharedtag1", new Tag() }
                        }))
            );

            manifest.Registry = "mcr.microsoft.com";
            platform1.Tags = new Dictionary<string, Tag>
            {
                { "tag1", new Tag() },
                { "tag2", new Tag
                    {
                        Syndication = new TagSyndication
                        {
                            Repo = syndicatedRepo2,
                            DestinationTags = new string[]
                            {
                                "tag2"
                            }
                        }
                    }
                },
            };

            platform2.Tags = new Dictionary<string, Tag>
            {
                { "tag3", new Tag() }
            };

            File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            Assert.True(manifest1Found);
            Assert.True(manifest2Found);
            manifestToolService
                .Verify(o => o.PushFromSpec(It.IsAny<string>(), false), Times.Exactly(2));

            ImageArtifactDetails expectedImageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(dockerfile1,
                                        simpleTags: new List<string>
                                        {
                                            "tag1",
                                            "tag2"
                                        })
                                },
                                Manifest = new ManifestData
                                {
                                    Digest = "mcr.microsoft.com/repo@digest",
                                    Created = manifestCreatedDate,
                                    SyndicatedDigests = new List<string>
                                    {
                                        "mcr.microsoft.com/repo2@digest"
                                    },
                                    SharedTags =
                                    {
                                        "sharedtag1",
                                        "sharedtag2"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string expectedOutput = JsonHelper.SerializeObject(expectedImageArtifactDetails);
            string actualOutput = File.ReadAllText(command.Options.ImageInfoPath);

            Assert.Equal(expectedOutput, actualOutput);
        }
    }
}
