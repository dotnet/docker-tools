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
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using IVssConnection = Microsoft.DotNet.ImageBuilder.Services.IVssConnection;
using WebApi = Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class GenerateEolAnnotationDataTests
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DateOnly _globalDate = DateOnly.FromDateTime(DateTime.UtcNow);
        private readonly DateOnly _specificDigestDate = new DateOnly(2022, 1, 1);

        public GenerateEolAnnotationDataTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task GenerateEolAnnotationData_RepoRemoved()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
                string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os2", tempFolderContext);
                string repo2Image1DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/os", tempFolderContext);

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
                                            simpleTags: new List<string>
                                            {
                                                "tag"
                                            },
                                            digest: "platformdigest101")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
                                        Digest = "imagedigest101"
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "tag"
                                            },
                                            digest: "platformdigest102")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
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
                                            simpleTags: new List<string>
                                            {
                                                "newtag"
                                            },
                                            digest : "platformdigest201")
                                    },
                                    ProductVersion = "2.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "2.0"
                                        },
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
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath);
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "imagedigest101" },
                        new EolDigestData { Digest = "platformdigest101" },
                        new EolDigestData { Digest = "imagedigest102" },
                        new EolDigestData { Digest = "platformdigest102" }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        [Fact]
        public async Task GenerateEolAnnotationData_ImageRemoved()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
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
                                            simpleTags: new List<string>
                                            {
                                                "1.0"
                                            },
                                            digest: "platformdigest101")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
                                        Digest = "imagedigest101"
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image2amd64DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "2.0"
                                            },
                                            digest: "platformdigest102-amd64"),
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "2.0"
                                            },
                                            digest: "platformdigest102-arm64")
                                    },
                                    ProductVersion = "2.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "2.0"
                                        },
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
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath);
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "platformdigest102-amd64" },
                        new EolDigestData { Digest = "platformdigest102-arm64" },
                        new EolDigestData { Digest = "imagedigest102" }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        [Fact]
        public async Task GenerateEolAnnotationData_DockerfileInSeveralImages_OnlyOneUpdated()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

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
                                            simpleTags: new List<string>
                                            {
                                                "1.0"
                                            },
                                            digest: "platformdigest101")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
                                        Digest = "imagedigest101"
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "2.0"
                                            },
                                            digest: "platformdigest102")
                                    },
                                    ProductVersion = "2.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "2.0"
                                        },
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
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath);
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "platformdigest102" },
                        new EolDigestData { Digest = "imagedigest102" }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        [Fact]
        public async Task GenerateEolAnnotationData_EolProduct()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

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
                                            simpleTags: new List<string>
                                            {
                                                "1.0"
                                            },
                                            digest: "platformdigest101")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
                                        Digest = "imagedigest101"
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "2.0"
                                            },
                                            digest: "platformdigest102")
                                    },
                                    ProductVersion = "2.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "2.0"
                                        },
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
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath,
                        dotNetReleasesServiceMock: CreateDotNetReleasesServiceMock(productEolDates));
                command.Options.AnnotateEolProducts = true;
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "platformdigest101" },
                        new EolDigestData { Digest = "imagedigest101" },
                        new EolDigestData { Digest = "imagedigest101-updated", EolDate = productEolDate },
                        new EolDigestData { Digest = "platformdigest101-updated", EolDate = productEolDate }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        [Fact]
        public async Task GenerateEolAnnotationData_DanglingDigests_UpdatedImageAndPlatform()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

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
                                            simpleTags: new List<string>
                                            {
                                                "1.0-amd64"
                                            },
                                            digest: "mcr.microsoft.com/repo1@platformdigest101")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
                                        Digest = "mcr.microsoft.com/repo1@imagedigest101"
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "2.0-amd64"
                                            },
                                            digest: "mcr.microsoft.com/repo1@platformdigest102")
                                    },
                                    ProductVersion = "2.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "2.0"
                                        },
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
                List<AcrEventEntry> imageEntries = new List<AcrEventEntry>
                {
                    new AcrEventEntry { Digest = "imagedigest101", TimeGenerated = DateTime.Today.AddDays(-5) },
                    new AcrEventEntry { Digest = "imagedigest101-dangling1", TimeGenerated = DateTime.Today.AddDays(-3) },
                    new AcrEventEntry { Digest = "imagedigest101-dangling2", TimeGenerated = DateTime.Today.AddDays(-2) },
                    new AcrEventEntry { Digest = "imagedigest101-updated", TimeGenerated = DateTime.Today },
                };

                List<AcrEventEntry> platformEntries = new List<AcrEventEntry>
                {
                    new AcrEventEntry { Digest = "platformdigest101-dangling1", TimeGenerated = DateTime.Today.AddDays(-5) },
                    new AcrEventEntry { Digest = "platformdigest101-dangling2", TimeGenerated = DateTime.Today.AddDays(-3) },
                    new AcrEventEntry { Digest = "platformdigest101-updated", TimeGenerated = DateTime.Today },
                };

                Dictionary<string, List<AcrEventEntry>> repoTagLogEntries = new Dictionary<string, List<AcrEventEntry>>
                {
                    { RepoTagIdentity("public/repo1", "1.0"), imageEntries },
                    { RepoTagIdentity("public/repo1", "1.0-amd64"), platformEntries }
                };

                GenerateEolAnnotationDataCommand command =
                    InitializeCommand(
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath,
                        azureLogServiceMock : CreateAzureLogServiceMock(repoTagLogEntries));
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@platformdigest101" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@platformdigest101-dangling1" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@platformdigest101-dangling2" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@imagedigest101" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@imagedigest101-dangling1" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@imagedigest101-dangling2" }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        [Fact]
        public async Task GenerateEolAnnotationData_DanglingDigests_ImageRemoved()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

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
                                            simpleTags: new List<string>
                                            {
                                                "1.0-amd64"
                                            },
                                            digest: "mcr.microsoft.com/repo1@platformdigest101")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
                                        Digest = "mcr.microsoft.com/repo1@imagedigest101"
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "2.0-amd64"
                                            },
                                            digest: "mcr.microsoft.com/repo1@platformdigest102")
                                    },
                                    ProductVersion = "2.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "2.0"
                                        },
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
                List<AcrEventEntry> imageEntries = new List<AcrEventEntry>
                {
                    new AcrEventEntry { Digest = "imagedigest101", TimeGenerated = DateTime.Today.AddDays(-5) },
                    new AcrEventEntry { Digest = "imagedigest101-dangling1", TimeGenerated = DateTime.Today.AddDays(-3) },
                    new AcrEventEntry { Digest = "imagedigest101-dangling2", TimeGenerated = DateTime.Today.AddDays(-2) },
                };

                List<AcrEventEntry> platformEntries = new List<AcrEventEntry>
                {
                    new AcrEventEntry { Digest = "platformdigest101-dangling1", TimeGenerated = DateTime.Today.AddDays(-5) },
                    new AcrEventEntry { Digest = "platformdigest101-dangling2", TimeGenerated = DateTime.Today.AddDays(-3) },
                };

                Dictionary<string, List<AcrEventEntry>> repoTagLogEntries = new Dictionary<string, List<AcrEventEntry>>
                {
                    { RepoTagIdentity("public/repo1", "1.0"), imageEntries },
                    { RepoTagIdentity("public/repo1", "1.0-amd64"), platformEntries }
                };

                GenerateEolAnnotationDataCommand command =
                    InitializeCommand(
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath,
                        azureLogServiceMock: CreateAzureLogServiceMock(repoTagLogEntries));
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@platformdigest101" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@platformdigest101-dangling1" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@platformdigest101-dangling2" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@imagedigest101" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@imagedigest101-dangling1" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@imagedigest101-dangling2" }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        [Fact]
        public async Task GenerateEolAnnotationData_DanglingDigests_PlatformRemoved()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1amd64DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/amd64", tempFolderContext);
                string repo1Image1arm64DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/arm64", tempFolderContext);
                string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/os", tempFolderContext);

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
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1amd64DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "1.0-amd64"
                                            },
                                            digest: "mcr.microsoft.com/repo1@platformdigest101"),
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1arm64DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "1.0-arm64"
                                            },
                                            digest: "mcr.microsoft.com/repo1@platformdigest102")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
                                        Digest = "mcr.microsoft.com/repo1@imagedigest101"
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "2.0-amd64"
                                            },
                                            digest: "mcr.microsoft.com/repo1@platformdigest102")
                                    },
                                    ProductVersion = "2.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "2.0"
                                        },
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
                List<AcrEventEntry> platformEntries = new List<AcrEventEntry>
                {
                    new AcrEventEntry { Digest = "platformdigest101-dangling1", TimeGenerated = DateTime.Today.AddDays(-5) },
                    new AcrEventEntry { Digest = "platformdigest101-dangling2", TimeGenerated = DateTime.Today.AddDays(-3) },
                };

                Dictionary<string, List<AcrEventEntry>> repoTagLogEntries = new Dictionary<string, List<AcrEventEntry>>
                {
                    { RepoTagIdentity("public/repo1", "1.0-amd64"), platformEntries }
                };

                GenerateEolAnnotationDataCommand command =
                    InitializeCommand(
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath,
                        azureLogServiceMock: CreateAzureLogServiceMock(repoTagLogEntries));
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@platformdigest101" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@platformdigest101-dangling1" },
                        new EolDigestData { Digest = "mcr.microsoft.com/repo1@platformdigest101-dangling2" }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        [Fact]
        public async Task GenerateEolAnnotationData_ImageAndPlatformUpdated()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

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
                                            simpleTags: new List<string>
                                            {
                                                "1.0"
                                            },
                                            digest: "platformdigest101")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
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
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath);
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "platformdigest101" },
                        new EolDigestData { Digest = "imagedigest101" }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        [Fact]
        public async Task GenerateEolAnnotationData_JustOnePlatformUpdated()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
                string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os2", tempFolderContext);

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
                                            simpleTags: new List<string>
                                            {
                                                "tag"
                                            },
                                            digest: "platformdigest101"),
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "tag2"
                                            },
                                            digest: "platformdigest102")
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "1.0"
                                        },
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
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath);
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "platformdigest102" }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        [Fact]
        public async Task GenerateEolAnnotationData_PlatformRemoved()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
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
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image2amd64DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "2.0"
                                            },
                                            digest: "platformdigest102-amd64"),
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "2.0"
                                            },
                                            digest: "platformdigest102-arm64")
                                    },
                                    ProductVersion = "2.0",
                                    Manifest = new ManifestData
                                    {
                                        SharedTags = new List<string>
                                        {
                                            "2.0"
                                        },
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
                        tempFolderContext,
                        oldImageInfoPath,
                        newImageInfoPath,
                        newEolDigestsListPath);
                await command.ExecuteAsync();

                EolAnnotationsData expectedEolAnnotations = new EolAnnotationsData
                {
                    EolDate = _globalDate,
                    EolDigests = new List<EolDigestData>
                    {
                        new EolDigestData { Digest = "platformdigest102-arm64" }
                    }
                };

                string expectedEolAnnotationsJson = JsonConvert.SerializeObject(expectedEolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                string actualEolDigestsJson = File.ReadAllText(newEolDigestsListPath);

                Assert.Equal(expectedEolAnnotationsJson, actualEolDigestsJson);
            }
        }

        private GenerateEolAnnotationDataCommand InitializeCommand(
            TempFolderContext tempFolderContext,
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

        private Mock<IDotNetReleasesService> CreateDotNetReleasesServiceMock(Dictionary<string, DateOnly?> productEolDates = null)
        {
            Mock<IDotNetReleasesService> dotNetReleasesServiceMock = new();
            dotNetReleasesServiceMock
                .Setup(o => o.GetProductEolDatesFromReleasesJson())
                .ReturnsAsync(productEolDates);

            return dotNetReleasesServiceMock;
        }

        private Mock<IAzureLogService> CreateAzureLogServiceMock(Dictionary<string, List<AcrEventEntry>> acrEventEntriesForRepoTags = null)
        {
            Mock<IAzureLogService> dotNetReleasesServiceMock = new();
            dotNetReleasesServiceMock
                .Setup(o => o.GetRecentPushEntries(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string repository, string tag) => {
                    return acrEventEntriesForRepoTags != null && acrEventEntriesForRepoTags.ContainsKey(RepoTagIdentity(repository, tag))
                        ? acrEventEntriesForRepoTags[RepoTagIdentity(repository, tag)]
                        : new List<AcrEventEntry>();
                } );

            return dotNetReleasesServiceMock;
        }

        private static string RepoTagIdentity(string repo, string tag) =>
            $"{repo}:{tag}";
    }
}
