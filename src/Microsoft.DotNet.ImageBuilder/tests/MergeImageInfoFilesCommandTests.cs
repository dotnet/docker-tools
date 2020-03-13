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
                                            CreatePlatform(
                                                repo2Image1Dockerfile,
                                                simpleTags: new List<string>
                                                {
                                                    "tag3"
                                                })
                                        }
                                    },
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(repo2Image2Dockerfile)
                                        }
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
                                        }
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
                                            CreatePlatform(
                                                repo2Image1Dockerfile,
                                                simpleTags: new List<string>
                                                {
                                                    "tag1"
                                                },
                                                baseImageDigest: "base1hash")
                                        }
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
                                        }
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
                                        }
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
                                        }
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
                            CreatePlatform(repo2Image1Dockerfile, new string[0])),
                        CreateImage(
                            CreatePlatform(repo2Image2Dockerfile, new string[0]))),
                    CreateRepo("repo3"),
                    CreateRepo("repo4",
                        CreateImage(
                            CreatePlatform(repo4Image2Dockerfile, new string[0])),
                        CreateImage(
                            CreatePlatform(repo4Image3Dockerfile, new string[0])))
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
                                        CreatePlatform(
                                            repo2Image1Dockerfile,
                                            simpleTags: new List<string>
                                            {
                                                "tag1",
                                                "tag3"
                                            },
                                            baseImageDigest: "base1hash")
                                    }
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
                                    }
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
                                    }
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
                                    }
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
            const string Os2 = "focal";
            const string Os1DisplayName = "Ubuntu 18.04";
            const string Os2DisplayName = "Ubuntu 20.04";

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
                                                OsVersion = Os1DisplayName,
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
                                                OsVersion = Os1DisplayName,
                                                OsType = OsType,
                                                Digest = "digest2",
                                                Dockerfile = dockerfile1,
                                                SimpleTags =
                                                {
                                                    "tag2"
                                                }
                                            }
                                        }
                                    },
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            new PlatformData
                                            {
                                                Architecture = "arm32",
                                                OsVersion = Os2DisplayName,
                                                OsType = OsType,
                                                Digest = "digest4",
                                                Dockerfile = dockerfile1,
                                                SimpleTags =
                                                {
                                                    "tagA"
                                                }
                                            }
                                        }
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
                                                OsVersion = Os1DisplayName,
                                                OsType = OsType,
                                                Digest = "digest2-new",
                                                Dockerfile = dockerfile1,
                                                SimpleTags =
                                                {
                                                    "tag3",
                                                    "tag2"
                                                }
                                            }
                                        }
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
                                                OsVersion = Os1DisplayName,
                                                OsType = OsType,
                                                Dockerfile = dockerfile2,
                                                SimpleTags =
                                                {
                                                    "tag1"
                                                }
                                            }
                                        }
                                    },
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            new PlatformData
                                            {
                                                Architecture = "arm32",
                                                OsVersion = Os2DisplayName,
                                                OsType = OsType,
                                                Digest = "digest4-new",
                                                Dockerfile = dockerfile1,
                                                SimpleTags =
                                                {
                                                    "tagB"
                                                }
                                            }
                                        }
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
                            CreatePlatform(dockerfile1, new string[0], osVersion: Os1, architecture: Architecture.ARM),
                            CreatePlatform(dockerfile1, new string[0], osVersion: Os1, architecture: Architecture.ARM64)),
                        CreateImage(
                            CreatePlatform(dockerfile1, new string[0], osVersion: Os2, architecture: Architecture.ARM)),
                        CreateImage(
                            CreatePlatform(dockerfile2, new string[0], osVersion: Os1)))
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
                                            OsVersion = Os1DisplayName,
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
                                            OsVersion = Os1DisplayName,
                                            OsType = OsType,
                                            Digest = "digest2-new",
                                            Dockerfile = dockerfile1,
                                            SimpleTags =
                                            {
                                                "tag2",
                                                "tag3"
                                            }
                                        }
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        new PlatformData
                                        {
                                            Architecture = "arm32",
                                            OsVersion = Os2DisplayName,
                                            OsType = OsType,
                                            Digest = "digest4-new",
                                            Dockerfile = dockerfile1,
                                            SimpleTags =
                                            {
                                                "tagA",
                                                "tagB"
                                            }
                                        }
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        new PlatformData
                                        {
                                            Architecture = "amd64",
                                            Digest = "digest3",
                                            OsVersion = Os1DisplayName,
                                            OsType = OsType,
                                            Dockerfile = dockerfile2,
                                            SimpleTags =
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

        private static string CreateDockerfile(string relativeDirectory, TempFolderContext context)
        {
            Directory.CreateDirectory(Path.Combine(context.Path, relativeDirectory));
            string dockerfileRelativePath = Path.Combine(relativeDirectory, "Dockerfile");
            File.WriteAllText(PathHelper.NormalizePath(Path.Combine(context.Path, dockerfileRelativePath)), "FROM base");
            return PathHelper.NormalizePath(dockerfileRelativePath);
        }
    }
}
