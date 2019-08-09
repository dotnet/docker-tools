// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class ImageInfoHelperTests
    {
        [Fact]
        public void ImageInfoHelper_MergeRepos_EmptyTarget()
        {
            RepoData[] repoDataSet = new RepoData[]
            {
                new RepoData
                {
                    Repo = "repo1",
                },
                new RepoData
                {
                    Repo = "repo2",
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            "image1",
                            new ImageData()
                        }
                    }
                }
            };

            List<RepoData> targetRepos = new List<RepoData>();
            ImageInfoHelper.MergeRepos(repoDataSet, targetRepos);

            CompareRepos(repoDataSet, targetRepos);
        }

        [Fact]
        public void ImageInfoHelper_MergeRepos_ExistingTarget()
        {
            ImageData repo2Image1;
            ImageData repo2Image2;
            ImageData repo2Image3;
            ImageData repo3Image1;

            RepoData[] repoDataSet = new RepoData[]
            {
                new RepoData
                {
                    Repo = "repo2",
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            "image1",
                            repo2Image1 = new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base1", "base1digest-NEW" }
                                }
                            }
                        }
                        ,
                        {
                            "image3",
                            repo2Image3 = new ImageData()
                        }
                    }
                },
                new RepoData
                {
                    Repo = "repo3",
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            "image1",
                            repo3Image1 = new ImageData()
                        }
                    }
                },
                new RepoData
                {
                    Repo = "repo4",
                }
            };

            List<RepoData> targetRepos = new List<RepoData>
            {
                new RepoData
                {
                    Repo = "repo1"
                },
                new RepoData
                {
                    Repo = "repo2",
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            "image1",
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base1", "base1digest" }
                                }
                            }
                        },
                        {
                            "image2",
                            repo2Image2 = new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base2", "base2digest" }
                                }
                            }
                        }
                    }
                },
                new RepoData
                {
                    Repo = "repo3"
                }
            };

            ImageInfoHelper.MergeRepos(repoDataSet, targetRepos);

            List<RepoData> expected = new List<RepoData>
            {
                new RepoData
                {
                    Repo = "repo1"
                },
                new RepoData
                {
                    Repo = "repo2",
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            "image1",
                            repo2Image1
                        },
                        {
                            "image2",
                            repo2Image2
                        },
                        {
                            "image3",
                            repo2Image3
                        }
                    }
                },
                new RepoData
                {
                    Repo = "repo3",
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            "image1",
                            repo3Image1
                        }
                    }
                },
                new RepoData
                {
                    Repo = "repo4",
                }
            };

            CompareRepos(expected, targetRepos);
        }

        /// <summary>
        /// Verifies that tags are removed from existing images in the target
        /// if the same tag doesn't exist in the source. This handles cases where
        /// a shared tag has been moved from one image to another.
        /// </summary>
        [Fact]
        public void ImageInfoHelper_MergeRepos_RemoveTag()
        {
            ImageData srcImage1;
            ImageData targetImage2;

            RepoData[] repoDataSet = new RepoData[]
            {
                new RepoData
                {
                    Repo = "repo1",
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            "image1",
                            srcImage1 = new ImageData
                            {
                                SimpleTags =
                                {
                                    "tag1",
                                    "tag3"
                                }
                            }
                        }
                    }
                }
            };

            List<RepoData> targetRepos = new List<RepoData>
            {
                new RepoData
                {
                    Repo = "repo1",
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            "image1",
                            new ImageData
                            {
                                SimpleTags =
                                {
                                    "tag1",
                                    "tag2",
                                    "tag4"
                                }
                            }
                        },
                        {
                            "image2",
                            targetImage2 = new ImageData
                            {
                                SimpleTags =
                                {
                                    "a"
                                }
                            }
                        }
                    }
                }
            };

            ImageInfoHelper.MergeRepos(repoDataSet, targetRepos);

            List<RepoData> expected = new List<RepoData>
            {
                new RepoData
                {
                    Repo = "repo1",
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            "image1",
                            srcImage1
                        },
                        {
                            "image2",
                            targetImage2
                        }
                    }
                }
            };

            CompareRepos(expected, targetRepos);
        }

        public static void CompareRepos(IList<RepoData> expected, IList<RepoData> actual)
        {
            Assert.Equal(JsonHelper.SerializeObject(expected), JsonHelper.SerializeObject(actual));
        }
    }
}
