// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Newtonsoft.Json;
using Shouldly;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public partial class MergeImageInfoFilesCommandTests
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

        [Fact]
        public async Task MergeImageInfoFilesCommand_CommitUrlOverride()
        {
            using TempFolderContext context = TestHelper.UseTempFolder();

            // Basic setup
            // 3 platforms/images
            // - static - is not updated, and should not have its commit URL overridden
            // - initial/updated - is updated, and should have its commit URL overridden with NewCommit

            const string StaticDigest = "sha256:static_digest";
            const string InitialDigest = "sha256:initial_digest";
            const string NewDigest = "sha256:new_digest";

            const string StaticCommit = "0000000000000000000000000000000000000000";
            const string InitialCommit = "1111111111111111111111111111111111111111";
            const string NewCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string CommitOverride = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            const string StaticDockerfilePath = "path/to/static";
            const string ChangingDockerfilePath = "path/to/changing";

            string staticDockerfile = CreateDockerfile(StaticDockerfilePath, context);
            string changingDockerfile = CreateDockerfile(ChangingDockerfilePath, context);

            var createCommitUrl = (string path, string commit) => $"https://github.com/dotnet/dotnet-docker/blob/{commit}/src/{path}/Dockerfile";

            var manifestFile = Path.Combine(context.Path, "manifest.json");
            Manifest manifest = CreateManifest(
                CreateRepo("repo",
                    CreateImage(
                        [
                            CreatePlatform(
                                dockerfilePath: staticDockerfile,
                                tags: [],
                                osVersion: "noble",
                                architecture: Architecture.AMD64
                            ),
                            CreatePlatform(
                                dockerfilePath: changingDockerfile,
                                tags: [],
                                osVersion: "noble",
                                architecture: Architecture.AMD64
                            )
                        ],
                        productVersion: "1.0"
                    )
                )
            );

            // Create initial image info with existing platform
            var staticPlatform = CreatePlatform(
                dockerfile: staticDockerfile,
                digest: StaticDigest,
                architecture: "amd64",
                osType: "Linux",
                osVersion: "noble",
                commitUrl: createCommitUrl(StaticDockerfilePath, StaticCommit)
            );

            var initialPlatform = CreatePlatform(
                dockerfile: changingDockerfile,
                digest: InitialDigest,
                architecture: "amd64",
                osType: "Linux",
                osVersion: "noble",
                commitUrl: createCommitUrl(ChangingDockerfilePath, InitialCommit)
            );

            var updatedPlatform = CreatePlatform(
                dockerfile: changingDockerfile,
                digest: NewDigest,
                architecture: "amd64",
                osType: "Linux",
                osVersion: "noble",
                commitUrl: createCommitUrl(ChangingDockerfilePath, NewCommit)
            );

            // Leave source image info dir empty for now.
            var sourceImageInfoDir = Path.Combine(context.Path, "infos");
            Directory.CreateDirectory(sourceImageInfoDir);

            var initialImageInfoFile = Path.Combine(sourceImageInfoDir, "initial-image-info.json");
            var initialImageInfo = new ImageArtifactDetails
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
                                Platforms = [initialPlatform, staticPlatform],
                                ProductVersion = "1.0"
                            }
                        }
                    }
                }
            };

            // Only the updated platform is new because the "static" image wasn't built in our scenario.
            var updatedImageInfoFile = Path.Combine(sourceImageInfoDir, "image-info.json");
            var updatedImageInfo = new ImageArtifactDetails
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
                                Platforms = [updatedPlatform],
                                ProductVersion = "1.0"
                            }
                        }
                    }
                }
            };

            File.WriteAllText(initialImageInfoFile, JsonHelper.SerializeObject(initialImageInfo));
            File.WriteAllText(updatedImageInfoFile, JsonHelper.SerializeObject(updatedImageInfo));
            File.WriteAllText(manifestFile, JsonConvert.SerializeObject(manifest));

            var outputImageInfoFile = Path.Combine(context.Path, "merged-image-info.json");

            MergeImageInfoCommand command = new MergeImageInfoCommand();
            command.Options.SourceImageInfoFolderPath = sourceImageInfoDir;
            command.Options.DestinationImageInfoPath = outputImageInfoFile;
            command.Options.InitialImageInfoPath = initialImageInfoFile;
            command.Options.CommitOverride = CommitOverride;
            command.Options.Manifest = manifestFile;
            command.LoadManifest();
            await command.ExecuteAsync();

            // Verify the merged result
            string resultContent = File.ReadAllText(outputImageInfoFile);
            ImageArtifactDetails mergedImageInfo = ImageArtifactDetails.FromJson(resultContent);

            mergedImageInfo.Repos.ShouldHaveSingleItem();
            mergedImageInfo.Repos[0].Images.ShouldHaveSingleItem();

            var platforms = mergedImageInfo.Repos[0].Images[0].Platforms;

            // Verify that we didn't lose any platforms during the merge
            platforms.ShouldContain(platform => platform.Dockerfile == staticDockerfile);
            platforms.ShouldContain(platform => platform.Dockerfile == changingDockerfile);

            // Verify that the static platform has the original commit URL
            var staticPlatformResult = platforms.FirstOrDefault(p => p.Dockerfile == staticDockerfile);
            var shaMatches = MergeImageInfoCommand.CommitShaRegex.Matches(staticPlatformResult.CommitUrl);
            shaMatches.ShouldHaveSingleItem();
            var staticCommitResult = shaMatches[0].Value;
            staticCommitResult.ShouldBe(StaticCommit);

            // Verify that the initial platform has the overridden commit
            var initialPlatformResult = platforms.FirstOrDefault(p => p.Dockerfile == changingDockerfile);
            var initialShaMatches = MergeImageInfoCommand.CommitShaRegex.Matches(initialPlatformResult.CommitUrl);
            initialShaMatches.ShouldHaveSingleItem();
            var initialCommitResult = initialShaMatches[0].Value;
            initialCommitResult.ShouldBe(CommitOverride);
        }
    }
}
