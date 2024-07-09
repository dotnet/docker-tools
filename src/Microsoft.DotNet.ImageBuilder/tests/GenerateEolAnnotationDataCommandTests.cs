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

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class GenerateEolAnnotationDataTests
    {
        private readonly DateOnly _globalDate = DateOnly.FromDateTime(DateTime.UtcNow);

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
                                        digest: "platformdigest101")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "imagedigest101"
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
                                        digest: "platformdigest102")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "imagedigest102"
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
                                        digest : "platformdigest201")
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = "imagedigest201"
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

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("imagedigest101"),
                    new("platformdigest101"),
                    new("imagedigest102"),
                    new("platformdigest102")
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0"
                                        ],
                                        digest: "platformdigest101")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "imagedigest101"
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
                                        digest: "platformdigest102-amd64"),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: "platformdigest102-arm64")
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = "imagedigest102"
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

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("platformdigest102-amd64"),
                    new("platformdigest102-arm64"),
                    new("imagedigest102")
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
                                        digest: "platformdigest101")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "imagedigest101"
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
                                        digest: "platformdigest102")
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = "imagedigest102"
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update image and platform digests in only one image that uses the shared Dockerfile
            imageArtifactDetails.Repos[0].Images[1].Manifest.Digest = "imagedigest102-updated";
            imageArtifactDetails.Repos[0].Images[1].Platforms[0].Digest = "platformdigest102-updated";

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("platformdigest102"),
                    new("imagedigest102")
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
                                        digest: "platformdigest101")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "imagedigest101"
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
                                        digest: "platformdigest102")
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = "imagedigest102"
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update image a platform digest for EOL product.
            imageArtifactDetails.Repos[0].Images[0].Manifest.Digest = "imagedigest101-updated";
            imageArtifactDetails.Repos[0].Images[0].Platforms[0].Digest = "platformdigest101-updated";


            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            DateOnly productEolDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
            Dictionary<string, DateOnly?> productEolDates = new()
            {
                { "1.0", productEolDate }
            };

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    dotNetReleasesServiceMock: CreateDotNetReleasesServiceMock(productEolDates));
            command.Options.AnnotateEolProducts = true;
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("platformdigest101"),
                    new("imagedigest101"),
                    new("imagedigest101-updated") { EolDate = productEolDate },
                    new("platformdigest101-updated") { EolDate = productEolDate }
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_DanglingDigests_UpdatedImageAndPlatform()
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
                                            "1.0-amd64"
                                        ],
                                        digest: "mcr.microsoft.com/repo1@platformdigest101")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "mcr.microsoft.com/repo1@imagedigest101"
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0-amd64"
                                        ],
                                        digest: "mcr.microsoft.com/repo1@platformdigest102")
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = "mcr.microsoft.com/repo1@imagedigest102"
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update first image and platform digests
            imageArtifactDetails.Repos[0].Images[0].Manifest.Digest = "mcr.microsoft.com/repo1@imagedigest101-updated";
            imageArtifactDetails.Repos[0].Images[0].Platforms[0].Digest = "mcr.microsoft.com/repo1@platformdigest101-updated";

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            // Prep test dangling digests
            // For image digest we have 3 entries for EOL, 2 of them are dangling,
            // old non-dangling digest is in of the entries.
            // For platform digest we have 2 entries for EOL, both dangling,
            // old non-dangling digest is not in the list.
            List<AcrEventEntry> imageEntries =
            [
                new(DateTime.Today.AddDays(-5), "imagedigest101"),
                new(DateTime.Today.AddDays(-3), "imagedigest101-dangling1"),
                new(DateTime.Today.AddDays(-2), "imagedigest101-dangling2"),
                new(DateTime.Today, "imagedigest101-updated"),
            ];

            List<AcrEventEntry> platformEntries =
            [
                new(DateTime.Today.AddDays(-5), "platformdigest101-dangling1"),
                new(DateTime.Today.AddDays(-3), "platformdigest101-dangling2"),
                new(DateTime.Today, "platformdigest101-updated"),
            ];

            Dictionary<string, List<AcrEventEntry>> repoTagLogEntries = new()
            {
                { RepoTagIdentity("public/repo1", "1.0"), imageEntries },
                { RepoTagIdentity("public/repo1", "1.0-amd64"), platformEntries }
            };

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    azureLogServiceMock: CreateAzureLogServiceMock(repoTagLogEntries));
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("mcr.microsoft.com/repo1@platformdigest101"),
                    new("mcr.microsoft.com/repo1@platformdigest101-dangling1"),
                    new("mcr.microsoft.com/repo1@platformdigest101-dangling2"),
                    new("mcr.microsoft.com/repo1@imagedigest101"),
                    new("mcr.microsoft.com/repo1@imagedigest101-dangling1"),
                    new("mcr.microsoft.com/repo1@imagedigest101-dangling2")
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_DanglingDigests_ImageRemoved()
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
                                            "1.0-amd64"
                                        ],
                                        digest: "mcr.microsoft.com/repo1@platformdigest101")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "mcr.microsoft.com/repo1@imagedigest101"
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0-amd64"
                                        ],
                                        digest: "mcr.microsoft.com/repo1@platformdigest102")
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = "mcr.microsoft.com/repo1@imagedigest102"
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove first image in first repo
            imageArtifactDetails.Repos[0].Images.RemoveAt(0);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            // Prep test dangling digests
            // For image digest we have 3 entries for EOL, 2 of them are dangling,
            // old non-dangling digest is in of the entries.
            // For platform digest we have 2 entries for EOL, both dangling,
            // old non-dangling digest is not in the list.
            // There are no new digests for image or platform as image was removed.
            List<AcrEventEntry> imageEntries =
            [
                new(DateTime.Today.AddDays(-5), "imagedigest101"),
                new(DateTime.Today.AddDays(-3), "imagedigest101-dangling1"),
                new(DateTime.Today.AddDays(-2), "imagedigest101-dangling2"),
            ];

            List<AcrEventEntry> platformEntries =
            [
                new(DateTime.Today.AddDays(-5), "platformdigest101-dangling1"),
                new(DateTime.Today.AddDays(-3), "platformdigest101-dangling2"),
            ];

            Dictionary<string, List<AcrEventEntry>> repoTagLogEntries = new()
            {
                { RepoTagIdentity("public/repo1", "1.0"), imageEntries },
                { RepoTagIdentity("public/repo1", "1.0-amd64"), platformEntries }
            };

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    azureLogServiceMock: CreateAzureLogServiceMock(repoTagLogEntries));
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("mcr.microsoft.com/repo1@platformdigest101"),
                    new("mcr.microsoft.com/repo1@platformdigest101-dangling1"),
                    new("mcr.microsoft.com/repo1@platformdigest101-dangling2"),
                    new("mcr.microsoft.com/repo1@imagedigest101"),
                    new("mcr.microsoft.com/repo1@imagedigest101-dangling1"),
                    new("mcr.microsoft.com/repo1@imagedigest101-dangling2")
                ]
            };

            string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

            Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
        }

        [Fact]
        public async Task GenerateEolAnnotationData_DanglingDigests_PlatformRemoved()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1amd64DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/amd64", tempFolderContext);
            string repo1Image1arm64DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/arm64", tempFolderContext);
            string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/os", tempFolderContext);

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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1amd64DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0-amd64"
                                        ],
                                        digest: "mcr.microsoft.com/repo1@platformdigest101"),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0-arm64"
                                        ],
                                        digest: "mcr.microsoft.com/repo1@platformdigest102")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "mcr.microsoft.com/repo1@imagedigest101"
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0-amd64"
                                        ],
                                        digest: "mcr.microsoft.com/repo1@platformdigest102")
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = "mcr.microsoft.com/repo1@imagedigest102"
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove first platform (in first image, first repo)
            imageArtifactDetails.Repos[0].Images[0].Platforms.RemoveAt(0);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            // Prep test dangling digests
            // For platform digest we have 2 entries for EOL, both dangling,
            // old non-dangling digest is not in the list.
            // There are no new digests for platform as it was removed.
            List<AcrEventEntry> platformEntries =
            [
                new(DateTime.Today.AddDays(-5), "platformdigest101-dangling1"),
                new(DateTime.Today.AddDays(-3), "platformdigest101-dangling2")
            ];

            Dictionary<string, List<AcrEventEntry>> repoTagLogEntries = new()
            {
                { RepoTagIdentity("public/repo1", "1.0-amd64"), platformEntries }
            };

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath,
                    azureLogServiceMock: CreateAzureLogServiceMock(repoTagLogEntries));
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("mcr.microsoft.com/repo1@platformdigest101"),
                    new("mcr.microsoft.com/repo1@platformdigest101-dangling1"),
                    new("mcr.microsoft.com/repo1@platformdigest101-dangling2")
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
                                        digest: "platformdigest101")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "imagedigest101"
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update and image and platform digests
            imageArtifactDetails.Repos[0].Images[0].Manifest.Digest = "imagedigest101-updated";
            imageArtifactDetails.Repos[0].Images[0].Platforms[0].Digest = "platformdigest101-updated";

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("platformdigest101"),
                    new("imagedigest101")
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
                                        digest: "platformdigest101"),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag2"
                                        ],
                                        digest: "platformdigest102")
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = "imagedigest101"
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update just one platform digest
            imageArtifactDetails.Repos[0].Images[0].Platforms[1].Digest = "platformdigest102-updated";

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            string newEolDigestsListPath = Path.Combine(tempFolderContext.Path, "eolDigests.json");

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("platformdigest102")
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
                                        digest: "platformdigest102-amd64"),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: "platformdigest102-arm64")
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = "imagedigest102"
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

            GenerateEolAnnotationDataCommand command =
                InitializeCommand(
                    oldImageInfoPath,
                    newImageInfoPath,
                    newEolDigestsListPath);
            await command.ExecuteAsync();

            EolAnnotationsData expectedEolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests =
                [
                    new("platformdigest102-arm64")
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
            string repoPrefix = "public/",
            Mock<IDotNetReleasesService> dotNetReleasesServiceMock = null,
            Mock<IAzureLogService> azureLogServiceMock = null)
        {
            Mock<ILoggerService> loggerServiceMock = new();
            dotNetReleasesServiceMock = dotNetReleasesServiceMock ?? CreateDotNetReleasesServiceMock();
            azureLogServiceMock = azureLogServiceMock ?? CreateAzureLogServiceMock();
            GenerateEolAnnotationDataCommand command = new(
                azureLogServiceMock.Object,
                dotNetReleasesServiceMock.Object,
                loggerServiceMock.Object);
            command.Options.OldImageInfoPath = oldImageInfoPath;
            command.Options.NewImageInfoPath = newImageInfoPath;
            command.Options.EolDigestsListPath = newEolDigestsListPath;
            command.Options.RepoPrefix = repoPrefix;
            return command;
        }

        private static Mock<IDotNetReleasesService> CreateDotNetReleasesServiceMock(Dictionary<string, DateOnly?> productEolDates = null)
        {
            Mock<IDotNetReleasesService> dotNetReleasesServiceMock = new();
            dotNetReleasesServiceMock
                .Setup(o => o.GetProductEolDatesFromReleasesJson())
                .ReturnsAsync(productEolDates);

            return dotNetReleasesServiceMock;
        }

        private static Mock<IAzureLogService> CreateAzureLogServiceMock(Dictionary<string, List<AcrEventEntry>> acrEventEntriesForRepoTags = null)
        {
            Mock<IAzureLogService> dotNetReleasesServiceMock = new();
            dotNetReleasesServiceMock
                .Setup(o => o.GetRecentPushEntries(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((string repository, string tag, string acrLogsWorkspaceId, int logsQueryDayRange) => {
                    return acrEventEntriesForRepoTags != null && acrEventEntriesForRepoTags.ContainsKey(RepoTagIdentity(repository, tag))
                        ? acrEventEntriesForRepoTags[RepoTagIdentity(repository, tag)]
                        : [];
                });

            return dotNetReleasesServiceMock;
        }

        private static string RepoTagIdentity(string repo, string tag) =>
            $"{repo}:{tag}";
    }
}
