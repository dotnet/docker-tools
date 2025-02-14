// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.ImageBuilder;
using Microsoft.DotNet.DockerTools.ImageBuilder.Commands;
using Microsoft.DotNet.DockerTools.ImageBuilder.Models.Image;
using Microsoft.DotNet.DockerTools.ImageBuilder.Tests.Helpers;
using Moq;
using Xunit;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Tests
{
    public class TrimUnchangedPlatformsCommandTests
    {
        [Fact]
        public async Task NoPlatforms()
        {
            await RunTestAsync(new ImageArtifactDetails(), new ImageArtifactDetails());
        }

        [Fact]
        public async Task MixOfCachedPlatforms()
        {
            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData(),
                                    new PlatformData { IsUnchanged = true },
                                }
                            },
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData { IsUnchanged = true },
                                }
                            },
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData(),
                                    new PlatformData(),
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo2",
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData { IsUnchanged = true },
                                }
                            },
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData { IsUnchanged = true },
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo3",
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData()
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails expectedImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData(),
                                }
                            },
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData(),
                                    new PlatformData(),
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo3",
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData()
                                }
                            }
                        }
                    }
                }
            };

            await RunTestAsync(imageArtifactDetails, expectedImageArtifactDetails);
        }

        [Fact]
        public async Task NoCachedPlatforms()
        {
            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData(),
                                }
                            },
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData(),
                                    new PlatformData(),
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo2",
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData()
                                }
                            }
                        }
                    }
                }
            };

            ImageArtifactDetails expectedImageArtifactDetails = new ImageArtifactDetails
            {
                Repos = new List<RepoData>
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData(),
                                }
                            },
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData(),
                                    new PlatformData(),
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo2",
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData()
                                }
                            }
                        }
                    }
                }
            };

            await RunTestAsync(imageArtifactDetails, expectedImageArtifactDetails);
        }

        private async Task RunTestAsync(ImageArtifactDetails input, ImageArtifactDetails expectedOutput)
        {
            using TempFolderContext tempFolderContext = new TempFolderContext();

            TrimUnchangedPlatformsCommand command = new TrimUnchangedPlatformsCommand(Mock.Of<ILoggerService>());
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "imageinfo.json");

            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(input));

            await command.ExecuteAsync();

            string expectedContents = JsonHelper.SerializeObject(expectedOutput);
            string actualContents = File.ReadAllText(command.Options.ImageInfoPath);

            Assert.Equal(expectedContents, actualContents);
        }
    }
}
