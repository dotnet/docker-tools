// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class MergeImageInfoFilesCommandTests
    {
        [Fact]
        public async Task MergeImageInfoFilesCommand_HappyPath()
        {
            using (TempFolderContext context = TestHelper.UseTempFolder())
            {
                string repo2Image1Dockerfile = CreateDockerfile("1.0/repo2/os1", context);
                string repo2Image2Dockerfile = CreateDockerfile("1.0/repo2/os2", context);
                string repo4Image2Dockerfile = CreateDockerfile("1.0/repo4/os2", context);
                string repo4Image3Dockerfile = CreateDockerfile("1.0/repo4/os3", context);

                PlatformData platform1 = CreatePlatform(
                    repo2Image1Dockerfile,
                    simpleTags: new List<string>
                    {
                        "tag3"
                    });

                PlatformData platform2 = CreatePlatform(
                    repo2Image1Dockerfile,
                    simpleTags: new List<string>
                    {
                        "tag1"
                    },
                    baseImageDigest: "base1hash");

                List<ImageArtifactDetails> imageArtifactDetailsList = new List<ImageArtifactDetails>
                {
                    new ImageArtifactDetails
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
                                            platform1
                                        },
                                        ProductVersion = "1.0"
                                    },
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(repo2Image2Dockerfile,
                                            simpleTags: new List<string>
                                            {
                                                "tag2"
                                            })
                                        },
                                        ProductVersion = "1.0"
                                    }
                                }
                            },
                            new RepoData
                            {
                                Repo = "repo4",
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                repo4Image2Dockerfile,
                                                simpleTags: new List<string>
                                                {
                                                    "tag1"
                                                })
                                        },
                                        ProductVersion = "1.0"
                                    }
                                }
                            }
                        }
                    },
                    new ImageArtifactDetails
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
                                            platform2
                                        },
                                        ProductVersion = "1.0"
                                    },
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                repo2Image2Dockerfile,
                                                simpleTags: new List<string>
                                                {
                                                    "tag2"
                                                })
                                        },
                                        ProductVersion = "1.0"
                                    }
                                }
                            },
                            new RepoData
                            {
                                Repo = "repo3",
                            },
                            new RepoData
                            {
                                Repo = "repo4",
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                repo4Image2Dockerfile,
                                                simpleTags: new List<string>
                                                {
                                                    "tag2"
                                                })
                                        },
                                        ProductVersion = "1.0"
                                    },
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                repo4Image3Dockerfile,
                                                simpleTags: new List<string>
                                                {
                                                    "tag1"
                                                })
                                        },
                                        ProductVersion = "1.0"
                                    }
                                }
                            }
                        }
                    }
                };

                MergeImageInfoCommand command = new MergeImageInfoCommand();
                command.Options.SourceImageInfoFolderPath = Path.Combine(context.Path, "image-infos");
                command.Options.DestinationImageInfoPath = Path.Combine(context.Path, "output.json");
                command.Options.Manifest = Path.Combine(context.Path, "manifest.json");

                Directory.CreateDirectory(command.Options.SourceImageInfoFolderPath);
                for (int i = 0; i < imageArtifactDetailsList.Count; i++)
                {
                    string file = Path.Combine(command.Options.SourceImageInfoFolderPath, $"{i}.json");
                    File.WriteAllText(file, JsonHelper.SerializeObject(imageArtifactDetailsList[i]));
                }

                Manifest manifest = CreateManifest(
                    CreateRepo("repo1"),
                    CreateRepo("repo2",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(repo2Image1Dockerfile, new string[] { "tag1" })
                            },
                            productVersion: "1.0"),
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(repo2Image2Dockerfile, new string[] { "tag2" })
                            },
                            productVersion: "1.0")),
                    CreateRepo("repo3"),
                    CreateRepo("repo4",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(repo4Image2Dockerfile, new string[] { "tag1" })
                            },
                            productVersion: "1.0"),
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(repo4Image3Dockerfile, new string[] { "tag2" })
                            },
                            productVersion: "1.0"))
                );
                File.WriteAllText(Path.Combine(context.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                string resultsContent = File.ReadAllText(command.Options.DestinationImageInfoPath);
                ImageArtifactDetails actual = JsonConvert.DeserializeObject<ImageArtifactDetails>(resultsContent);

                PlatformData expectedPlatform = CreatePlatform(
                    repo2Image1Dockerfile,
                    simpleTags: new List<string>
                    {
                        "tag1",
                        "tag3"
                    },
                    baseImageDigest: "base1hash");

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
                                        expectedPlatform
                                    },
                                    ProductVersion = "1.0"
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            repo2Image2Dockerfile,
                                            simpleTags: new List<string>
                                            {
                                                "tag2"
                                            })
                                    },
                                    ProductVersion = "1.0"
                                }
                            }
                        },
                        new RepoData
                        {
                            Repo = "repo3",
                        },
                        new RepoData
                        {
                            Repo = "repo4",
                            Images =
                            {
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            repo4Image2Dockerfile,
                                            simpleTags: new List<string>
                                            {
                                                "tag1",
                                                "tag2"
                                            })
                                    },
                                    ProductVersion = "1.0"
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            repo4Image3Dockerfile,
                                            simpleTags: new List<string>
                                            {
                                                "tag1"
                                            })
                                    },
                                    ProductVersion = "1.0"
                                }
                            }
                        }
                    }
                };

                ImageInfoHelperTests.CompareImageArtifactDetails(expected, actual);
            }
        }

        [Fact]
        public async Task MergeImageInfoFilesCommand_DuplicateDockerfilePaths()
        {
            const string OsType = "Linux";
            const string Os1 = "bionic";
            const string Os2 = "noble";

            using (TempFolderContext context = TestHelper.UseTempFolder())
            {
                string dockerfile1 = CreateDockerfile("1.0/repo1/os1", context);
                string dockerfile2 = CreateDockerfile("1.0/repo1/os2", context);

                List<ImageArtifactDetails> imageArtifactDetailsList = new List<ImageArtifactDetails>
                {
                    new ImageArtifactDetails
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
                                                Architecture = "arm32",
                                                OsVersion = Os1,
                                                OsType = OsType,
                                                Digest = "digest1",
                                                Dockerfile = dockerfile1,
                                                SimpleTags =
                                                {
                                                    "tag1",
                                                    "tag2"
                                                }
                                            },
                                            new PlatformData
                                            {
                                                Architecture = "arm64",
                                                OsVersion = Os1,
                                                OsType = OsType,
                                                Digest = "digest2",
                                                Dockerfile = dockerfile1,
                                                SimpleTags =
                                                {
                                                    "tag2"
                                                }
                                            }
                                        },
                                        ProductVersion = "1.0"
                                    },
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            new PlatformData
                                            {
                                                Architecture = "arm32",
                                                OsVersion = Os2,
                                                OsType = OsType,
                                                Digest = "digest4",
                                                Dockerfile = dockerfile1,
                                                SimpleTags =
                                                {
                                                    "tagA"
                                                }
                                            }
                                        },
                                        ProductVersion = "1.0"
                                    }
                                }
                            }
                        }
                    },
                    new ImageArtifactDetails
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
                                                Architecture = "arm64",
                                                OsVersion = Os1,
                                                OsType = OsType,
                                                Digest = "digest2-new",
                                                Dockerfile = dockerfile1,
                                                SimpleTags =
                                                {
                                                    "tag3",
                                                    "tag2"
                                                }
                                            }
                                        },
                                        ProductVersion = "1.0"
                                    }
                                }
                            }
                        }
                    },
                    new ImageArtifactDetails
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
                                                Architecture = "amd64",
                                                Digest = "digest3",
                                                OsVersion = Os1,
                                                OsType = OsType,
                                                Dockerfile = dockerfile2,
                                                SimpleTags =
                                                {
                                                    "tag1"
                                                }
                                            }
                                        },
                                        ProductVersion = "1.0"
                                    },
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            new PlatformData
                                            {
                                                Architecture = "arm32",
                                                OsVersion = Os2,
                                                OsType = OsType,
                                                Digest = "digest4-new",
                                                Dockerfile = dockerfile1,
                                                SimpleTags =
                                                {
                                                    "tagB"
                                                }
                                            }
                                        },
                                        ProductVersion = "1.0"
                                    }
                                }
                            }
                        }
                    }
                };

                MergeImageInfoCommand command = new MergeImageInfoCommand();
                command.Options.SourceImageInfoFolderPath = Path.Combine(context.Path, "image-infos");
                command.Options.DestinationImageInfoPath = Path.Combine(context.Path, "output.json");
                command.Options.Manifest = Path.Combine(context.Path, "manifest.json");

                Directory.CreateDirectory(command.Options.SourceImageInfoFolderPath);
                for (int i = 0; i < imageArtifactDetailsList.Count; i++)
                {
                    string file = Path.Combine(command.Options.SourceImageInfoFolderPath, $"{i}.json");
                    File.WriteAllText(file, JsonHelper.SerializeObject(imageArtifactDetailsList[i]));
                }

                Manifest manifest = CreateManifest(
                    CreateRepo("repo1",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(dockerfile1, new string[] { "tag1" }, osVersion: Os1, architecture: Architecture.ARM),
                                CreatePlatform(dockerfile1, new string[] { "tag2" }, osVersion: Os1, architecture: Architecture.ARM64)
                            },
                            productVersion: "1.0"),
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(dockerfile1, new string[] { "tag3" }, osVersion: Os2, architecture: Architecture.ARM)
                            },
                            productVersion: "1.0"),
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(dockerfile2, new string[] { "tag4" }, osVersion: Os1)
                            },
                            productVersion: "1.0"))
                );
                File.WriteAllText(Path.Combine(context.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                string resultsContent = File.ReadAllText(command.Options.DestinationImageInfoPath);
                ImageArtifactDetails actual = JsonConvert.DeserializeObject<ImageArtifactDetails>(resultsContent);

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
                                            Architecture = "arm32",
                                            OsVersion = Os1,
                                            OsType = OsType,
                                            Digest = "digest1",
                                            Dockerfile = dockerfile1,
                                            SimpleTags =
                                            {
                                                "tag1",
                                                "tag2"
                                            }
                                        },
                                        new PlatformData
                                        {
                                            Architecture = "arm64",
                                            OsVersion = Os1,
                                            OsType = OsType,
                                            Digest = "digest2-new",
                                            Dockerfile = dockerfile1,
                                            SimpleTags =
                                            {
                                                "tag2",
                                                "tag3"
                                            }
                                        }
                                    },
                                    ProductVersion = "1.0"
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        new PlatformData
                                        {
                                            Architecture = "arm32",
                                            OsVersion = Os2,
                                            OsType = OsType,
                                            Digest = "digest4-new",
                                            Dockerfile = dockerfile1,
                                            SimpleTags =
                                            {
                                                "tagA",
                                                "tagB"
                                            }
                                        }
                                    },
                                    ProductVersion = "1.0"
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        new PlatformData
                                        {
                                            Architecture = "amd64",
                                            Digest = "digest3",
                                            OsVersion = Os1,
                                            OsType = OsType,
                                            Dockerfile = dockerfile2,
                                            SimpleTags =
                                            {
                                                "tag1"
                                            }
                                        }
                                    },
                                    ProductVersion = "1.0"
                                }
                            }
                        }
                    }
                };

                ImageInfoHelperTests.CompareImageArtifactDetails(expected, actual);
            }
        }

        [Fact]
        public async Task MergeImageInfoFilesCommand_SourceFolderPathNotFound()
        {
            MergeImageInfoCommand command = new MergeImageInfoCommand();
            command.Options.SourceImageInfoFolderPath = "foo";
            command.Options.DestinationImageInfoPath = "output.json";

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => command.ExecuteAsync());
        }

        [Fact]
        public async Task MergeImageInfoFilesCommand_SourceFolderEmpty()
        {
            using (TempFolderContext context = TestHelper.UseTempFolder())
            {
                ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
                {
                    Repos =
                    {
                        new RepoData { Repo = "repo" }
                    }
                };

                // Store the content in a .txt file which the command should NOT be looking for.
                File.WriteAllText("image-info.txt", JsonHelper.SerializeObject(imageArtifactDetails));

                MergeImageInfoCommand command = new MergeImageInfoCommand();
                command.Options.SourceImageInfoFolderPath = context.Path;
                command.Options.DestinationImageInfoPath = "output.json";

                await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());
            }
        }

        /// <summary>
        /// Verifies the command will replace any existing tags or syndicated digests of a merged image.
        /// </summary>
        /// <remarks>
        /// See https://github.com/dotnet/docker-tools/pull/269
        /// </remarks>
        [Fact]
        public async Task MergeImageInfoFilesCommand_Publish_ReplaceContent()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo2Image2DockerfilePath = CreateDockerfile("2.0/runtime/os", tempFolderContext);
            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        [
                            CreatePlatform(repo1Image1DockerfilePath, [ "tag1" ])
                        ],
                        productVersion: "1.0")),
                CreateRepo("repo2",
                    CreateImage(
                        [
                            CreatePlatform(repo2Image2DockerfilePath, [ "tag1" ])
                        ],
                        productVersion: "2.0"))
            );

            RepoData repo2;

            ImageArtifactDetails srcImageArtifactDetails = new()
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
                                    CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "newtag"
                                        ])
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SyndicatedDigests =
                                    [
                                        "newdigest1",
                                        "newdigest2"
                                    ]
                                }
                            }
                        }
                    },
                    {
                        repo2 = new RepoData
                        {
                            Repo = "repo2",
                            Images =
                            {
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(repo2Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag1"
                                        ])
                                    },
                                    ProductVersion = "2.0"
                                }
                            }
                        }
                    }
                }
            };

            string file = Path.Combine(tempFolderContext.Path, "image-info.json");
            File.WriteAllText(file, JsonHelper.SerializeObject(srcImageArtifactDetails));

            ImageArtifactDetails targetImageArtifactDetails = new()
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
                                    CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "oldtag"
                                        ])
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SyndicatedDigests =
                                    [
                                        "olddigest1",
                                        "olddigest2"
                                    ]
                                }
                            }
                        }
                    }
                }
            };

            MergeImageInfoCommand command = new();
            command.Options.SourceImageInfoFolderPath = Path.Combine(tempFolderContext.Path, "image-infos");
            command.Options.DestinationImageInfoPath = Path.Combine(tempFolderContext.Path, "output.json");
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.IsPublishScenario = true;

            Directory.CreateDirectory(command.Options.SourceImageInfoFolderPath);

            string srcImageArtifactDetailsPath = Path.Combine(command.Options.SourceImageInfoFolderPath, "src.json");
            File.WriteAllText(srcImageArtifactDetailsPath, JsonHelper.SerializeObject(srcImageArtifactDetails));

            string targetImageArtifactDetailsPath = Path.Combine(command.Options.SourceImageInfoFolderPath, "target.json");
            File.WriteAllText(targetImageArtifactDetailsPath, JsonHelper.SerializeObject(targetImageArtifactDetails));

            command.Options.InitialImageInfoPath = targetImageArtifactDetailsPath;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

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
                                    CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "newtag"
                                        ])
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SyndicatedDigests =
                                    [
                                        "newdigest1",
                                        "newdigest2"
                                    ]
                                }
                            }
                        }
                    },
                    repo2
                }
            };

            string resultsContent = File.ReadAllText(command.Options.DestinationImageInfoPath);
            ImageArtifactDetails actual = JsonConvert.DeserializeObject<ImageArtifactDetails>(resultsContent);

            ImageInfoHelperTests.CompareImageArtifactDetails(expectedImageArtifactDetails, actual);
        }

        /// <summary>
        /// Verifies the command will remove any out-of-date content that exists within the target image info file,
        /// meaning that it has content which isn't reflected in the manifest.
        /// </summary>
        [Fact]
        public async Task MergeImageInfoFilesCommand_Publish_RemoveOutOfDateContent()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo2Image2DockerfilePath = CreateDockerfile("2.0/runtime/os", tempFolderContext);
            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        [
                            CreatePlatform(repo1Image1DockerfilePath, [])
                        ],
                        productVersion: "1.0")),
                CreateRepo("repo2",
                    CreateImage(
                        [
                            CreatePlatform(repo2Image2DockerfilePath, [])
                        ],
                        productVersion: "2.0"))
            );
            manifest.Registry = "mcr.microsoft.com";

            RepoData repo2;

            ImageArtifactDetails srcImageArtifactDetails = new()
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
                                    CreatePlatform(repo1Image1DockerfilePath)
                                },
                                ProductVersion = "1.0"
                            }
                        }
                    },
                    {
                        repo2 = new RepoData
                        {
                            Repo = "repo2",
                            Images =
                            {
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(repo2Image2DockerfilePath)
                                    },
                                    ProductVersion = "2.0"
                                }
                            }
                        }
                    }
                }
            };

            string file = Path.Combine(tempFolderContext.Path, "image-info.json");
            File.WriteAllText(file, JsonHelper.SerializeObject(srcImageArtifactDetails));

            ImageArtifactDetails targetImageArtifactDetails = new()
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
                                    CreatePlatform(repo1Image1DockerfilePath)
                                },
                                ProductVersion = "1.0"
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(
                                        CreateDockerfile("1.0/runtime2/os", tempFolderContext))
                                },
                                ProductVersion = "1.0"
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo4"
                    }
                }
            };

            MergeImageInfoCommand command = new();
            command.Options.SourceImageInfoFolderPath = Path.Combine(tempFolderContext.Path, "image-infos");
            command.Options.DestinationImageInfoPath = Path.Combine(tempFolderContext.Path, "output.json");
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.IsPublishScenario = true;

            Directory.CreateDirectory(command.Options.SourceImageInfoFolderPath);

            string srcImageArtifactDetailsPath = Path.Combine(command.Options.SourceImageInfoFolderPath, "src.json");
            File.WriteAllText(srcImageArtifactDetailsPath, JsonHelper.SerializeObject(srcImageArtifactDetails));

            string targetImageArtifactDetailsPath = Path.Combine(command.Options.SourceImageInfoFolderPath, "target.json");
            File.WriteAllText(targetImageArtifactDetailsPath, JsonHelper.SerializeObject(targetImageArtifactDetails));

            command.Options.InitialImageInfoPath = targetImageArtifactDetailsPath;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

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
                                    CreatePlatform(repo1Image1DockerfilePath)
                                },
                                ProductVersion = "1.0"
                            }
                        }
                    },
                    repo2
                }
            };

            string resultsContent = File.ReadAllText(command.Options.DestinationImageInfoPath);
            ImageArtifactDetails actual = JsonConvert.DeserializeObject<ImageArtifactDetails>(resultsContent);

            ImageInfoHelperTests.CompareImageArtifactDetails(expectedImageArtifactDetails, actual);
        }
    }
}
