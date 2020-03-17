// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class ImageInfoHelperTests
    {
        [Fact]
        public void ImageInfoHelper_MergeRepos_ImageDigest()
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
                                Platforms =
                                {
                                    {
                                        repo2Image1 = new PlatformData
                                        {
                                            Dockerfile = "image1",
                                            BaseImageDigest = "base1digest-NEW"
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
                                Platforms =
                                {
                                    new PlatformData
                                    {
                                        Dockerfile = "image1",
                                        BaseImageDigest = "base1digest"
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
                                SharedTags = new List<string>
                                {
                                    "shared1",
                                    "shared2"
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
                                SharedTags = new List<string>
                                {
                                    "shared2",
                                    "shared3"
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
                                SharedTags = new List<string>
                                {
                                    "shared1",
                                    "shared2",
                                    "shared3"
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
                                    {
                                        srcPlatform1 = new PlatformData
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
                                SharedTags = new List<string>
                                {
                                    "sharedtag1"
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
                                SharedTags = new List<string>
                                {
                                    "sharedtag2"
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
                                    srcPlatform1,
                                    targetPlatform2
                                },
                                SharedTags = new List<string>
                                {
                                    "sharedtag1"
                                }
                            }
                        }
                    }
                }
            };

            CompareImageArtifactDetails(expected, targetImageArtifactDetails);
        }

        public static void CompareImageArtifactDetails(ImageArtifactDetails expected, ImageArtifactDetails actual)
        {
            Assert.Equal(JsonHelper.SerializeObject(expected), JsonHelper.SerializeObject(actual));
        }
    }
}
