// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;

using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ContainerRegistryHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class GenerateEolAnnotationDataTests
    {
        private const string DefaultRepoPrefix = "public/";
        private const string AcrName = "myacr.azurecr.io";
        private const string McrName = "mcr.microsoft.com";
        private readonly DateOnly _globalDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Additional test scenarios:
        // *  exclusion of digests which are already annotated
        // * 

        [Fact]
        public async Task GenerateEolAnnotationData_RepoRemoved()
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

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
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
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) }
                        }),
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo2",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest201", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest201", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101")) { Tag = "1.0" },
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102")) { Tag = "1.0" },
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101")) { Tag = "tag" },
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102")) { Tag = "tag" },
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_ImageRemoved()
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

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
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
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-amd64", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-arm64", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) }
                        })
                ]);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102")) { Tag = "2.0" },
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-amd64")) { Tag = "2.0" },
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-arm64")) { Tag = "2.0" },
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_ExcludeDigestsThatAreAlreadyAnnotated()
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

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
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
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            string armDigest = DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-arm64");

            // Set the Arm64 digest as already annotated. This should exclude it from the list of digests to annotate.
            Manifest lifecycleArtifactManifest;
            Mock<ILifecycleMetadataService> lifecycleMetadataServiceMock = new();
            lifecycleMetadataServiceMock
                .Setup(o => o.IsDigestAnnotatedForEol(armDigest, It.IsAny<ILoggerService>(), It.IsAny<bool>(), out lifecycleArtifactManifest))
                .Returns(true);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-amd64", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-arm64", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) }
                        })
                ]);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory,
                    lifecycleMetadataService: lifecycleMetadataServiceMock.Object);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102")) { Tag = "2.0" },
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-amd64")) { Tag = "2.0" },
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_DockerfileInSeveralImages_OnlyOneUpdated()
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

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
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
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
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

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102")),
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102")),
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_EolProduct()
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
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "3.0-preview"
                                        ],
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest103"))
                                },
                                ProductVersion = "3.0-preview",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "3.0-preview"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest103")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update image a platform digest for EOL product.
            imageArtifactDetails.Repos[0].Images[0].Manifest.Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101-updated");
            imageArtifactDetails.Repos[0].Images[0].Platforms[0].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101-updated");


            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            DateOnly productEolDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
            Dictionary<string, DateOnly> productEolDates = new()
            {
                { "1.0", productEolDate }
            };

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101"),
                            CreateArtifactManifestProperties(digest: "platformdigest101-updated", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101"),
                            CreateArtifactManifestProperties(digest: "imagedigest101-updated", tags: ["1.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest101-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101-updated", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory,
                    dotNetReleasesService: CreateDotNetReleasesService(productEolDates));
            command.Options.AnnotateEolProducts = true;
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101")),
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101-updated")) { EolDate = productEolDate, Tag = "1.0" },
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101")),
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101-updated")) { EolDate = productEolDate, Tag = "1.0" }
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        // https://github.com/dotnet/docker-tools/issues/1507
        [Fact]
        public async Task GenerateEolAnnotationData_EolProduct_DigestMissingFromRegistry()
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
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "3.0-preview"
                                        ],
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest103"))
                                },
                                ProductVersion = "3.0-preview",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "3.0-preview"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest103")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update image and platform digest for non-EOL product.
            imageArtifactDetails.Repos[0].Images[1].Manifest.Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102-updated");
            imageArtifactDetails.Repos[0].Images[1].Platforms[0].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            DateOnly productEolDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
            Dictionary<string, DateOnly> productEolDates = new()
            {
                { "1.0", productEolDate }
            };

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest102"),
                            CreateArtifactManifestProperties(digest: "platformdigest102-updated", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102"),
                            CreateArtifactManifestProperties(digest: "imagedigest102-updated", tags: ["1.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102-updated", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory,
                    dotNetReleasesService: CreateDotNetReleasesService(productEolDates));
            command.Options.AnnotateEolProducts = true;
            await command.ExecuteAsync();

            // The key part of this test is that the digests defined here do NOT include the 101 digests (the EOL
            // product) because those digests were defined to NOT be in the registry.
            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102")),
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102"))
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_ImageAndPlatformUpdated()
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

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101"),
                            CreateArtifactManifestProperties(digest: "platformdigest101-updated", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101"),
                            CreateArtifactManifestProperties(digest: "imagedigest101-updated", tags: ["1.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest101-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101-updated", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101")),
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101")),
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_JustOnePlatformUpdated()
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

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102"),
                            CreateArtifactManifestProperties(digest: "platformdigest102-updated", tags: ["tag2"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102"))
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_DoNotReturnAnnotationDigest()
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

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
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
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
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

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102"))
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_PlatformRemoved()
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

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest102-amd64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-arm64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["2.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            IContainerRegistryContentClientFactory registryContentClientFactory = CreateContainerRegistryContentClientFactory(AcrName,
                [
                    CreateContainerRegistryContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest102-amd64", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-arm64", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    registryContentClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-arm64")) { Tag = "2.0" },
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        private static GenerateEolAnnotationDataCommand InitializeCommand(
            string oldImageInfoPath,
            string newImageInfoPath,
            string newEolDigestsListPath,
            IContainerRegistryClientFactory registryClientFactory,
            IContainerRegistryContentClientFactory registryContentClientFactory,
            string repoPrefix = DefaultRepoPrefix,
            ILifecycleMetadataService lifecycleMetadataService = null,
            IDotNetReleasesService dotNetReleasesService = null)
        {
            Mock<ILoggerService> loggerServiceMock = new();
            lifecycleMetadataService = lifecycleMetadataService ?? CreateLifecycleMetadataService([]);
            dotNetReleasesService = dotNetReleasesService ?? CreateDotNetReleasesService();
            GenerateEolAnnotationDataCommand command = new(
                dotNetReleasesService: dotNetReleasesService,
                loggerService: loggerServiceMock.Object,
                acrClientFactory: registryClientFactory,
                acrContentClientFactory: registryContentClientFactory,
                tokenCredentialProvider: Mock.Of<IAzureTokenCredentialProvider>(),
                registryCredentialsProvider: Mock.Of<IRegistryCredentialsProvider>(),
                lifecycleMetadataService: lifecycleMetadataService);
            command.Options.OldImageInfoPath = oldImageInfoPath;
            command.Options.NewImageInfoPath = newImageInfoPath;
            command.Options.EolDigestsListPath = newEolDigestsListPath;
            command.Options.RegistryOptions = new() { RepoPrefix = repoPrefix, Registry = AcrName };
            return command;
        }

        private static ILifecycleMetadataService CreateLifecycleMetadataService(Dictionary<string, bool> digestAnnotatedMapping)
        {
            Mock<ILifecycleMetadataService> lifecycleMetadataServiceMock = new();
            Manifest lifecycleArtifactManifest;
            lifecycleMetadataServiceMock
                .Setup(o => o.IsDigestAnnotatedForEol(It.IsAny<string>(), It.IsAny<ILoggerService>(), It.IsAny<bool>(), out lifecycleArtifactManifest))
                .Returns(false);

            foreach (KeyValuePair<string, bool> digestAnnotated in digestAnnotatedMapping)
            { 
                lifecycleMetadataServiceMock
                    .Setup(o => o.IsDigestAnnotatedForEol(digestAnnotated.Key, It.IsAny<ILoggerService>(), It.IsAny<bool>(), out lifecycleArtifactManifest))
                    .Returns(digestAnnotated.Value);
            }
            
            return lifecycleMetadataServiceMock.Object;
        }

        private static IDotNetReleasesService CreateDotNetReleasesService(Dictionary<string, DateOnly> productEolDates = null)
        {
            Mock<IDotNetReleasesService> dotNetReleasesServiceMock = new();
            dotNetReleasesServiceMock
                .Setup(o => o.GetProductEolDatesFromReleasesJson())
                .ReturnsAsync(productEolDates);

            return dotNetReleasesServiceMock.Object;
        }

        private static string RepoTagIdentity(string repo, string tag) =>
            $"{repo}:{tag}";
    }
}
