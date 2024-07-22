// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;

using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ContainerRegistryHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class GenerateEolAnnotationDataTests
    {
        private const string AcrName = "myacr.azurecr.io";
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo2", digest: "platformdigest201"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo2", digest: "imagedigest201")
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
                    CreateContainerRepository("repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["1.0"])
                        ]),
                    CreateContainerRepository("repo2",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest201", tags: ["newtag"]),
                            CreateArtifactManifestProperties(digest: "imagedigest201", tags: ["2.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")) { Tag = "1.0" },
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")) { Tag = "1.0" },
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101")) { Tag = "tag" },
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102")) { Tag = "tag" },
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-amd64")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-arm64"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")
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
                    CreateContainerRepository("repo1",
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

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")) { Tag = "2.0" },
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-amd64")) { Tag = "2.0" },
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-arm64")) { Tag = "2.0" },
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-amd64")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-arm64"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")
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
                    CreateContainerRepository("repo1",
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

            string armDigest = DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-arm64");

            // Set the Arm64 digest as already annotated. This should exclude it from the list of digests to annotate.
            Mock<IOrasService> orasServiceMock = new();
            orasServiceMock
                .Setup(o => o.IsDigestAnnotatedForEol(armDigest, It.IsAny<ILoggerService>(), It.IsAny<bool>()))
                .Returns(true);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    orasService: orasServiceMock.Object);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")) { Tag = "2.0" },
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-amd64")) { Tag = "2.0" },
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update image and platform digests in only one image that uses the shared Dockerfile
            imageArtifactDetails.Repos[0].Images[1].Manifest.Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102-updated");
            imageArtifactDetails.Repos[0].Images[1].Platforms[0].Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
                [
                    CreateContainerRepository("repo1",
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

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")),
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102")),
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update image a platform digest for EOL product.
            imageArtifactDetails.Repos[0].Images[0].Manifest.Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101-updated");
            imageArtifactDetails.Repos[0].Images[0].Platforms[0].Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101-updated");


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
                    CreateContainerRepository("repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101"),
                            CreateArtifactManifestProperties(digest: "platformdigest101-updated", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101"),
                            CreateArtifactManifestProperties(digest: "imagedigest101-updated", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["2.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory,
                    dotNetReleasesService: CreateDotNetReleasesService(productEolDates));
            command.Options.AnnotateEolProducts = true;
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")),
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101-updated")) { EolDate = productEolDate, Tag = "1.0" },
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101")),
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101-updated")) { EolDate = productEolDate, Tag = "1.0" }
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update and image and platform digests
            imageArtifactDetails.Repos[0].Images[0].Manifest.Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101-updated");
            imageArtifactDetails.Repos[0].Images[0].Platforms[0].Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
                [
                    CreateContainerRepository("repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101"),
                            CreateArtifactManifestProperties(digest: "platformdigest101-updated", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101"),
                            CreateArtifactManifestProperties(digest: "imagedigest101-updated", tags: ["1.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")),
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101")),
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest101")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag2"
                                        ],
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update just one platform digest
            imageArtifactDetails.Repos[0].Images[0].Platforms[1].Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            Mock<IContainerRegistryClient> registryClientMock = CreateContainerRegistryClientMock(
                [
                    CreateContainerRepository("repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102"),
                            CreateArtifactManifestProperties(digest: "platformdigest102-updated", tags: ["tag2"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102"))
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
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-amd64")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-arm64"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(AcrName, "repo1", digest: "imagedigest102")
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
                    CreateContainerRepository("repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest102-amd64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-arm64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["2.0"]),
                        ])
                ]);
            IContainerRegistryClientFactory registryClientFactory = CreateContainerRegistryClientFactory(
                AcrName, registryClientMock.Object);

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    registryClientFactory);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new(DockerHelper.GetImageName(AcrName, "repo1", digest: "platformdigest102-arm64")) { Tag = "2.0" },
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
            string repoPrefix = "public/",
            IOrasService orasService = null,
            IDotNetReleasesService dotNetReleasesService = null)
        {
            Mock<ILoggerService> loggerServiceMock = new();
            orasService = orasService ?? CreateOrasService([]);
            dotNetReleasesService = dotNetReleasesService ?? CreateDotNetReleasesService();
            GenerateEolAnnotationDataCommand command = new(
                dotNetReleasesService,
                loggerServiceMock.Object,
                registryClientFactory,
                Mock.Of<IAzureTokenCredentialProvider>(),
                orasService);
            command.Options.OldImageInfoPath = oldImageInfoPath;
            command.Options.NewImageInfoPath = newImageInfoPath;
            command.Options.EolDigestsListPath = newEolDigestsListPath;
            command.Options.RepoPrefix = repoPrefix;
            command.Options.RegistryName = AcrName;
            return command;
        }

        private static IOrasService CreateOrasService(Dictionary<string, bool> digestAnnotatedMapping)
        {
            Mock<IOrasService> orasServiceMock = new();

            orasServiceMock
                .Setup(o => o.IsDigestAnnotatedForEol(It.IsAny<string>(), It.IsAny<ILoggerService>(), It.IsAny<bool>()))
                .Returns(false);

            foreach (KeyValuePair<string, bool> digestAnnotated in digestAnnotatedMapping)
            { 
                orasServiceMock
                    .Setup(o => o.IsDigestAnnotatedForEol(digestAnnotated.Key, It.IsAny<ILoggerService>(), It.IsAny<bool>()))
                    .Returns(digestAnnotated.Value);
            }
            
            return orasServiceMock.Object;
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
