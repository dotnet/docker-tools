// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Xunit;

using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class ImageInfoHelperTests
    {
        [Fact]
        public void LoadFromContent()
        {
            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "runtime",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "1.0/runtime/linux/Dockerfile",
                                        OsType = "Linux",
                                        OsVersion = "Ubuntu 20.04",
                                        Architecture = "amd64",
                                        SimpleTags = new List<string> { "linux" }
                                    },
                                    new PlatformData
                                    {
                                        Dockerfile = "1.0/runtime/windows/Dockerfile",
                                        OsType = "Windows",
                                        OsVersion = "Windows Server, version 2004",
                                        Architecture = "amd64",
                                        SimpleTags = new List<string> { "windows" }
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags = new List<string>
                                    {
                                        "shared"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            using TempFolderContext tempFolderContext = new TempFolderContext();

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                CreateDockerfile("1.0/runtime/linux", tempFolderContext),
                                new string[] { "linux" },
                                OS.Linux,
                                "focal"),
                            CreatePlatform(
                                CreateDockerfile("1.0/runtime/windows", tempFolderContext),
                                new string[] { "windows" },
                                OS.Windows,
                                "nanoserver-2004")
                        },
                        new Dictionary<string, Tag>
                        {
                            { "shared", new Tag() }
                        }))
            );

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));

            ManifestInfo manifestInfo = ManifestInfo.Load(new FakeManifestOptions(manifestPath));
            string expected = JsonHelper.SerializeObject(imageArtifactDetails);

            ImageArtifactDetails result = ImageInfoHelper.LoadFromContent(expected, manifestInfo);

            Assert.Equal(expected, JsonHelper.SerializeObject(result));
            RepoData repo = result.Repos.First();
            ImageData image = repo.Images.First();
            RepoInfo expectedRepo = manifestInfo.AllRepos.First();
            ImageInfo expectedImage = expectedRepo.AllImages.First();
            Assert.Same(expectedImage, image.ManifestImage);
            Assert.Same(expectedRepo, image.ManifestRepo);

            Assert.Same(expectedImage, image.Platforms.First().ImageInfo);
            Assert.Same(expectedImage.AllPlatforms.First(), image.Platforms.First().PlatformInfo);
            Assert.Same(expectedImage.AllPlatforms.Last(), image.Platforms.Last().PlatformInfo);
        }

        [Fact]
        public void LoadFromContent_ImagesDifferByPatchVersion()
        {
            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "runtime",
                        Images =
                        {
                            new ImageData
                            {
                                ProductVersion = "1.0.0",
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "1.0/runtime/linux/Dockerfile",
                                        OsType = "Linux",
                                        OsVersion = "Ubuntu 20.04",
                                        Architecture = "amd64",
                                        SimpleTags = new List<string> { "linux" }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            using TempFolderContext tempFolderContext = new TempFolderContext();

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                CreateDockerfile("1.0/runtime/linux", tempFolderContext),
                                new string[] { "linux" },
                                OS.Linux,
                                "focal")
                        },
                        productVersion: "1.0.1"))
            );

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));

            ManifestInfo manifestInfo = ManifestInfo.Load(new FakeManifestOptions(manifestPath));
            string expected = JsonHelper.SerializeObject(imageArtifactDetails);

            ImageArtifactDetails result = ImageInfoHelper.LoadFromContent(expected, manifestInfo);

            Assert.Equal(expected, JsonHelper.SerializeObject(result));
            RepoData repo = result.Repos.First();
            ImageData image = repo.Images.First();
            RepoInfo expectedRepo = manifestInfo.AllRepos.First();
            ImageInfo expectedImage = expectedRepo.AllImages.First();
            Assert.Same(expectedImage, image.ManifestImage);
            Assert.Same(expectedRepo, image.ManifestRepo);

            Assert.Same(expectedImage, image.Platforms.First().ImageInfo);
            Assert.Same(expectedImage.AllPlatforms.First(), image.Platforms.First().PlatformInfo);
            Assert.Same(expectedImage.AllPlatforms.Last(), image.Platforms.Last().PlatformInfo);
        }

        [Fact]
        public void ImageInfoHelper_MergeRepos_ImageDigest()
        {
            ImageInfo imageInfo1 = CreateImageInfo();

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
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1",
                                        Digest = "digest"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails
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
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageInfoHelper.MergeImageArtifactDetails(imageArtifactDetails, targetImageArtifactDetails);
            CompareImageArtifactDetails(imageArtifactDetails, targetImageArtifactDetails);
        }

        [Fact]
        public void ImageInfoHelper_MergeRepos_EmptyTarget()
        {
            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
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
                                        Dockerfile = "image1"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails();
            ImageInfoHelper.MergeImageArtifactDetails(imageArtifactDetails, targetImageArtifactDetails);

            CompareImageArtifactDetails(imageArtifactDetails, targetImageArtifactDetails);
        }

        [Fact]
        public void ImageInfoHelper_MergeRepos_ExistingTarget()
        {
            PlatformData repo2Image1;
            PlatformData repo2Image2;
            PlatformData repo2Image3;
            PlatformData repo3Image1;

            DateTime oldCreatedDate = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            DateTime newCreatedDate = DateTime.Now;

            ImageInfo imageInfo1 = CreateImageInfo();
            ImageInfo imageInfo2 = CreateImageInfo();

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo2",
                        Images =
                        {
                            new ImageData
                            {
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    {
                                        repo2Image1 = new PlatformData
                                        {
                                            Dockerfile = "image1",
                                            BaseImageDigest = "base1digest-NEW",
                                            Created = newCreatedDate
                                        }
                                    },
                                    {
                                        repo2Image3  = new PlatformData
                                        {
                                            Dockerfile = "image3"
                                        }
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
                                ManifestImage = imageInfo2,
                                Platforms =
                                {
                                    {
                                        repo3Image1 = new PlatformData
                                        {
                                            Dockerfile = "image1"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo4",
                    }
                }
            };

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1"
                    },
                    new RepoData
                    {
                        Repo = "repo2",
                        Images =
                        {
                            new ImageData
                            {
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1",
                                        BaseImageDigest = "base1digest",
                                        Created = oldCreatedDate
                                    },
                                    {
                                        repo2Image2 = new PlatformData
                                        {
                                            Dockerfile = "image2",
                                            BaseImageDigest = "base2digest"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo3"
                    }
                }
            };

            ImageInfoHelper.MergeImageArtifactDetails(imageArtifactDetails, targetImageArtifactDetails);

            ImageArtifactDetails expected = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1"
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
                                    repo2Image1,
                                    repo2Image2,
                                    repo2Image3
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
                                    repo3Image1
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo4",
                    }
                }
            };

            CompareImageArtifactDetails(expected, targetImageArtifactDetails);
        }

        /// <summary>
        /// Verifies that tags are merged between the source and destination.
        /// </summary>
        [Fact]
        public void ImageInfoHelper_MergeRepos_MergeTags()
        {
            PlatformData srcImage1;
            PlatformData targetImage2;

            ImageInfo imageInfo1 = CreateImageInfo();

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
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    {
                                        srcImage1 = new PlatformData
                                        {
                                            Dockerfile = "image1",
                                            SimpleTags =
                                            {
                                                "tag1",
                                                "tag3"
                                            }
                                        }
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "shared1",
                                        "shared2"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails
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
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1",
                                        SimpleTags =
                                        {
                                            "tag1",
                                            "tag2",
                                            "tag4"
                                        }
                                    },
                                    {
                                        targetImage2 = new PlatformData
                                        {
                                            Dockerfile = "image2",
                                            SimpleTags =
                                            {
                                                "a"
                                            }
                                        }
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "shared2",
                                        "shared3"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageInfoHelper.MergeImageArtifactDetails(imageArtifactDetails, targetImageArtifactDetails);

            ImageArtifactDetails expected = new ImageArtifactDetails
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
                                        Dockerfile = "image1",
                                        SimpleTags =
                                        {
                                            "tag1",
                                            "tag2",
                                            "tag3",
                                            "tag4"
                                        }
                                    },
                                    targetImage2
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "shared1",
                                        "shared2",
                                        "shared3"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            CompareImageArtifactDetails(expected, targetImageArtifactDetails);
        }

        /// <summary>
        /// Verifies that tags are removed from existing images in the target
        /// if the same tag doesn't exist in the source. This handles cases where
        /// a shared tag has been moved from one image to another.
        /// </summary>
        [Fact]
        public void ImageInfoHelper_MergeRepos_RemoveTag()
        {
            PlatformData srcPlatform1;
            PlatformData targetPlatform2;

            ImageInfo imageInfo1 = CreateImageInfo();

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
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    {
                                        srcPlatform1 = new PlatformData
                                        {
                                            Dockerfile = "image1",
                                            SimpleTags =
                                            {
                                                "tag3",
                                                "tag1"
                                            }
                                        }
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag1b",
                                        "sharedtag1a",
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails
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
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    {
                                        new PlatformData
                                        {
                                            Dockerfile = "image1",
                                            SimpleTags =
                                            {
                                                "tag1",
                                                "tag2",
                                                "tag4"
                                            }
                                        }
                                    },
                                    {
                                        targetPlatform2 = new PlatformData
                                        {
                                            Dockerfile = "image2",
                                            SimpleTags =
                                            {
                                                "a"
                                            }
                                        }
                                    }
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag2",
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageInfoMergeOptions options = new ImageInfoMergeOptions
            {
                ReplaceTags = true
            };

            ImageInfoHelper.MergeImageArtifactDetails(imageArtifactDetails, targetImageArtifactDetails, options);

            ImageArtifactDetails expected = new ImageArtifactDetails
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
                                        Dockerfile = "image1",
                                        SimpleTags =
                                        {
                                            "tag1",
                                            "tag3"
                                        }
                                    },
                                    targetPlatform2
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    {
                                        "sharedtag1a",
                                        "sharedtag1b",
                                    }
                                }
                            }
                        }
                    }
                }
            };

            CompareImageArtifactDetails(expected, targetImageArtifactDetails);
        }

        [Fact]
        public void Merge_DuplicatedPlatforms()
        {
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
                                ManifestImage = CreateImageInfo(),
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails
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
                                ManifestImage = CreateImageInfo(),
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1",
                                        SimpleTags = new List<string>
                                        {
                                            "tag1"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

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
                                    new PlatformData
                                    {
                                        Dockerfile = "image1"
                                    }
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1",
                                        SimpleTags = new List<string>
                                        {
                                            "tag1"
                                        }
                                    }
                                }
                            },
                        }
                    }
                }
            };

            ImageInfoHelper.MergeImageArtifactDetails(imageArtifactDetails, targetImageArtifactDetails);
            CompareImageArtifactDetails(expectedImageArtifactDetails, targetImageArtifactDetails);
        }

        [Fact]
        public void Merge_SharedDockerfile_DistinctPlatform()
        {
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
                                ProductVersion = "1.0",
                                ManifestImage = CreateImageInfo(),
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        OsType = "Linux",
                                        OsVersion = "Ubuntu 19.04",
                                        Architecture = "amd64",
                                        Dockerfile = "1.0/runtime/os/Dockerfile",
                                        SimpleTags = new List<string>
                                        {
                                            "tag1"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails
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
                                ProductVersion = "2.0",
                                ManifestImage = CreateImageInfo(),
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        OsType = "Linux",
                                        OsVersion = "Ubuntu 19.04",
                                        Architecture = "amd64",
                                        Dockerfile = "1.0/runtime/os/Dockerfile",
                                        SimpleTags = new List<string>
                                        {
                                            "tag2"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

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
                                ProductVersion = "1.0",
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        OsType = "Linux",
                                        OsVersion = "Ubuntu 19.04",
                                        Architecture = "amd64",
                                        Dockerfile = "1.0/runtime/os/Dockerfile",
                                        SimpleTags = new List<string>
                                        {
                                            "tag1"
                                        }
                                    }
                                }
                            },
                            new ImageData
                            {
                                ProductVersion = "2.0",
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        OsType = "Linux",
                                        OsVersion = "Ubuntu 19.04",
                                        Architecture = "amd64",
                                        Dockerfile = "1.0/runtime/os/Dockerfile",
                                        SimpleTags = new List<string>
                                        {
                                            "tag2"
                                        }
                                    }
                                }
                            },
                        }
                    }
                }
            };

            using TempFolderContext tempFolderContext = new TempFolderContext();

            Manifest manifest = CreateManifest(
                CreateRepo("repo",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(CreateDockerfile("1.0/runtime/os", tempFolderContext), new string[] { "tag1" })
                        },
                        productVersion: "1.0"),
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(CreateDockerfile("1.0/runtime/os", tempFolderContext), new string[] { "tag2" })
                        },
                        productVersion: "2.0")));

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));

            ManifestInfo manifestInfo = ManifestInfo.Load(new FakeManifestOptions(manifestPath));

            ImageArtifactDetails source = ImageInfoHelper.LoadFromContent(JsonHelper.SerializeObject(imageArtifactDetails), manifestInfo);
            ImageArtifactDetails target = ImageInfoHelper.LoadFromContent(JsonHelper.SerializeObject(targetImageArtifactDetails), manifestInfo);

            ImageInfoHelper.MergeImageArtifactDetails(source, target);
            CompareImageArtifactDetails(expectedImageArtifactDetails, target);
        }

        /// <summary>
        /// Tests the scenario where a source image defines a manifest that the target doesn't have.
        /// </summary>
        [Fact]
        public void ImageInfoHelper_MergeRepos_NewManifest()
        {
            ImageInfo imageInfo1 = CreateImageInfo();

            ImageArtifactDetails srcImageArtifactDetails = new ImageArtifactDetails
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
                                ManifestImage = imageInfo1,
                                Manifest = new ManifestData
                                {
                                    SharedTags = new List<string>
                                    {
                                        "shared"
                                    }
                                },
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1",
                                        Digest = "digest"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails
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
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageInfoHelper.MergeImageArtifactDetails(srcImageArtifactDetails, targetImageArtifactDetails);
            CompareImageArtifactDetails(srcImageArtifactDetails, targetImageArtifactDetails);
        }

        /// <summary>
        /// Tests the scenario where a target image defines a manifest that the source doesn't have.
        /// </summary>
        [Fact]
        public void ImageInfoHelper_MergeRepos_RemovedManifest()
        {
            ImageInfo imageInfo1 = CreateImageInfo();

            ImageArtifactDetails srcImageArtifactDetails = new ImageArtifactDetails
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
                                ManifestImage = imageInfo1,
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1",
                                        Digest = "digest"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails
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
                                ManifestImage = imageInfo1,
                                Manifest = new ManifestData
                                {
                                    SharedTags = new List<string>
                                    {
                                        "shared"
                                    }
                                },
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            ImageInfoHelper.MergeImageArtifactDetails(srcImageArtifactDetails, targetImageArtifactDetails);
            CompareImageArtifactDetails(srcImageArtifactDetails, targetImageArtifactDetails);
        }

        public static void CompareImageArtifactDetails(ImageArtifactDetails expected, ImageArtifactDetails actual)
        {
            Assert.Equal(JsonHelper.SerializeObject(expected), JsonHelper.SerializeObject(actual));
        }

        private static ImageInfo CreateImageInfo()
        {
            return ImageInfo.Create(
                new Image
                {
                    Platforms = Array.Empty<Platform>()
                },
                "fullrepo",
                "repo",
                new ManifestFilter(),
                new VariableHelper(new Manifest(), Mock.Of<IManifestOptionsInfo>(), null),
                "base");
        }

        private class FakeManifestOptions : IManifestOptionsInfo
        {
            public FakeManifestOptions(string manifestPath)
            {
                Manifest = manifestPath;
            }

            public string Manifest { get; }

            public string RegistryOverride => null;

            public string RepoPrefix => null;

            public IDictionary<string, string> Variables { get; } = new Dictionary<string, string>();

            public bool IsDryRun => false;

            public bool IsVerbose => false;

            public ManifestFilter GetManifestFilter() => new ManifestFilter();

            public string GetOption(string name) => throw new NotImplementedException();
        }
    }
}
