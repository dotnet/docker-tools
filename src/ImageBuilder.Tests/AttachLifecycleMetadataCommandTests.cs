#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ContainerRegistryHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class AttachLifecycleMetadataCommandTests
    {
        #nullable enable annotations
        public TestContext? TestContext { get; set; }

        #nullable disable annotations

        private const string DefaultRepoPrefix = "public/";
        private const string AcrName = "myacr.azurecr.io";
        private const string McrName = "mcr.microsoft.com";
        private readonly DateOnly _globalDate = DateOnly.FromDateTime(DateTime.UtcNow);
        private readonly ConcurrentBag<string> _annotatedDigests = [];
        private readonly Mock<ILifecycleMetadataService> _lifecycleMetadataServiceMock = new();

        [TestMethod]
        public async Task AttachLifecycleMetadata_RepoRemoved()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os2", tempFolderContext);
            string repo2Image1DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo2Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "newtag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo2", digest: "platformdigest201"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo2", digest: "imagedigest201")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove 'repo1' repo, with its images
            imageArtifactDetails.Repos.RemoveAt(0);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["1.0"])
                        ]),
                    CreateContainerRepository($"{DefaultRepoPrefix}repo2",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest201", tags: ["newtag"]),
                            CreateArtifactManifestProperties(digest: "imagedigest201", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) }
                        }),
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo2",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest201", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest201", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_ImageRemoved()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2amd64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/amd64", tempFolderContext);
            string repo1Image2arm64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/arm64", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2amd64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-amd64")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-arm64"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove second image from 'repo1' repo
            imageArtifactDetails.Repos[0].Images.RemoveAt(1);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-amd64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-arm64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-amd64", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-arm64", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) }
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-amd64"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-arm64"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_ExcludeDigestsThatAreAlreadyAnnotated()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2amd64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/amd64", tempFolderContext);
            string repo1Image2arm64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/arm64", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2amd64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-amd64")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-arm64"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove second image from 'repo1' repo
            imageArtifactDetails.Repos[0].Images.RemoveAt(1);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-amd64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-arm64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            string armDigest = DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-arm64");

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-amd64", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-arm64", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) }
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory,
                    // Already annotated, so it should be skipped.
                    annotatedDigests: [armDigest]);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-amd64"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);

            // An earlier EOL date on an already-annotated image must never fail the run.
            _lifecycleMetadataServiceMock.Verify(
                o => o.AttachLifecycleMetadataAsync(
                    It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<bool>(), true, It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_DockerfileInSeveralImages_OnlyOneUpdated()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update image and platform digests in only one image that uses the shared Dockerfile
            imageArtifactDetails.Repos[0].Images[1].Manifest.Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102-updated");
            imageArtifactDetails.Repos[0].Images[1].Platforms[0].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102"),
                            CreateArtifactManifestProperties(digest: "platformdigest102-updated", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102"),
                            CreateArtifactManifestProperties(digest: "imagedigest102-updated", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102-updated", new ManifestQueryResult(string.Empty, []) }
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_ImageAndPlatformUpdated()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update and image and platform digests
            imageArtifactDetails.Repos[0].Images[0].Manifest.Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101-updated");
            imageArtifactDetails.Repos[0].Images[0].Platforms[0].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101"),
                            CreateArtifactManifestProperties(digest: "platformdigest101-updated", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101"),
                            CreateArtifactManifestProperties(digest: "imagedigest101-updated", tags: ["1.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest101-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101-updated", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_JustOnePlatformUpdated()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os2", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag2"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update just one platform digest
            imageArtifactDetails.Repos[0].Images[0].Platforms[1].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102"),
                            CreateArtifactManifestProperties(digest: "platformdigest102-updated", tags: ["tag2"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102")
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_DoNotReturnAnnotationDigest()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os2", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag2"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update just one platform digest
            imageArtifactDetails.Repos[0].Images[0].Platforms[1].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102"),
                            CreateArtifactManifestProperties(digest: "platformdigest102-updated", tags: ["tag2"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "annotationdigest"),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            // Define a subject field in this manifest to indicate it is a referrer, not an image manifest
                            { "annotationdigest", new ManifestQueryResult(string.Empty, new JsonObject { { "subject", "" } }) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102")
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_PlatformRemoved()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image2amd64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/amd64", tempFolderContext);
            string repo1Image2arm64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/arm64", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2amd64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-amd64")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-arm64"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove one platform
            imageArtifactDetails.Repos[0].Images[0].Platforms.RemoveAt(1);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest102-amd64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-arm64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest102-amd64", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-arm64", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-arm64"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_ManifestDeletedDuringEnumeration_Skipped()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags: ["tag"],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags = ["1.0"],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove all images so everything in the registry is considered unsupported
            imageArtifactDetails.Repos.Clear();

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Registry lists three manifests, but one will return 404 when fetched (simulating concurrent deletion)
            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "deleteddigest", tags: ["old"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            // Set up content client mock where "deleteddigest" throws a 404
            Mock<IAcrContentClient> contentClientMock = CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                {
                    { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                    { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                });
            contentClientMock
                .Setup(o => o.GetManifestAsync("deleteddigest", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(404, "manifest not found"));

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [contentClientMock]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        private AttachLifecycleMetadataCommand InitializeCommand(
            IAcrClientFactory registryClientFactory,
            IAcrContentClientFactory registryContentClientFactory,
            IEnumerable<string> annotatedDigests = null,
            bool annotationSucceeds = true,
            IMarImageIngestionReporter ingestionReporter = null)
        {
            _lifecycleMetadataServiceMock
                .Setup(o => o.AttachLifecycleMetadataAsync(
                    It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<bool>(),
                    It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string digest, DateOnly date, bool _, bool _, CancellationToken _) =>
                {
                    if (!annotationSucceeds)
                    {
                        throw new InvalidOperationException($"Failed to attach lifecycle metadata to '{digest}'.");
                    }

                    _annotatedDigests.Add(digest);
                    return new LifecycleMetadataAttachmentResult.Attached(
                        LifecycleArtifactHelper.CreateLifecycleArtifact($"{digest}-lifecycle", date));
                });

            foreach (string digest in annotatedDigests ?? [])
            {
                // Model an image marked EOL by an earlier run, so its EOL date differs from today's.
                _lifecycleMetadataServiceMock
                    .Setup(o => o.AttachLifecycleMetadataAsync(
                        digest, _globalDate, false, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new LifecycleMetadataAttachmentResult.ConflictSkipped(
                        LifecycleArtifactHelper.CreateLifecycleArtifact(
                            $"{digest}-lifecycle", _globalDate.AddDays(-30))));
            }

            return new AttachLifecycleMetadataCommand(
                logger: Mock.Of<ILogger<AttachLifecycleMetadataCommand>>(),
                acrClientFactory: registryClientFactory,
                acrContentClientFactory: registryContentClientFactory,
                lifecycleMetadataService: _lifecycleMetadataServiceMock.Object,
                ingestionReporter: ingestionReporter ?? Mock.Of<IMarImageIngestionReporter>(),
                // Image info paths are absolute, so the artifact root isn't used.
                artifactService: TestHelper.CreateArtifactService(Path.GetTempPath()));
        }

        private static UnsupportedLifecycleMetadataOptions CreatePublishedOptions(
            string oldImageInfoPath,
            string newImageInfoPath) =>
            new()
            {
                OldImageInfoPath = oldImageInfoPath,
                NewImageInfoPath = newImageInfoPath,
                RegistryOptions = new() { RepoPrefix = DefaultRepoPrefix, Registry = AcrName }
            };

        private static IAcrContentClientFactory CreateSingleImageContentClientFactory(string repo, string digest) =>
            CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock(repo,
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { digest, new ManifestQueryResult(string.Empty, []) },
                        })
                ]);
    }
}
