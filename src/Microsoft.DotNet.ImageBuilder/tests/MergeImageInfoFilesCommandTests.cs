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

                List<RepoData[]> repoDataSets = new List<RepoData[]>
                {
                    new RepoData[]
                    {
                        new RepoData
                        {
                            Repo = "repo1"
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
                                        new PlatformData
                                        {
                                            Path = repo2Image1Dockerfile,
                                            SimpleTags =
                                            {
                                                "tag3"
                                            }
                                        }
                                    }
                                },
                                new ImageData
                                {
                                    Platforms = new List<PlatformData>
                                    {
                                        new PlatformData
                                        {
                                            Path = repo2Image2Dockerfile
                                        }
                                    }
                                }
                            }
                        },
                        new RepoData
                        {
                            Repo = "repo4",
                            Images = new List<ImageData>
                            {
                                new ImageData
                                {
                                    Platforms = new List<PlatformData>
                                    {
                                        new PlatformData
                                        {
                                            Path = repo4Image2Dockerfile,
                                            SimpleTags =
                                            {
                                                "tag1"
                                            }
                                        }
                                    }
                                }
                            }
                        },
                    },
                    new RepoData[]
                    {
                        new RepoData
                        {
                            Repo = "repo2",
                            Images = new List<ImageData>
                            {
                                new ImageData
                                {
                                    Platforms = new List<PlatformData>
                                    {
                                        new PlatformData
                                        {
                                            Path = repo2Image1Dockerfile,
                                            BaseImages = new SortedDictionary<string, string>
                                            {
                                                { "base1", "base1hash" }
                                            },
                                            SimpleTags =
                                            {
                                                "tag1"
                                            }
                                        }
                                    }
                                },
                                new ImageData
                                {
                                    Platforms = new List<PlatformData>
                                    {
                                        new PlatformData
                                        {
                                            Path = repo2Image2Dockerfile,
                                            SimpleTags =
                                            {
                                                "tag2"
                                            }
                                        }
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
                            Images = new List<ImageData>
                            {
                                new ImageData
                                {
                                    Platforms = new List<PlatformData>
                                    {
                                        new PlatformData
                                        {
                                            Path = repo4Image2Dockerfile,
                                            SimpleTags =
                                            {
                                                "tag2"
                                            }
                                        }
                                    }
                                },
                                new ImageData
                                {
                                    Platforms = new List<PlatformData>
                                    {
                                        new PlatformData
                                        {
                                            Path = repo4Image3Dockerfile,
                                            SimpleTags =
                                            {
                                                "tag1"
                                            }
                                        }
                                    }
                                }
                            }
                        },
                    }
                };

                MergeImageInfoCommand command = new MergeImageInfoCommand();
                command.Options.SourceImageInfoFolderPath = Path.Combine(context.Path, "image-infos");
                command.Options.DestinationImageInfoPath = Path.Combine(context.Path, "output.json");
                command.Options.Manifest = Path.Combine(context.Path, "manifest.json");

                Directory.CreateDirectory(command.Options.SourceImageInfoFolderPath);
                for (int i = 0; i < repoDataSets.Count; i++)
                {
                    string file = Path.Combine(command.Options.SourceImageInfoFolderPath, $"{i}.json");
                    File.WriteAllText(file, JsonHelper.SerializeObject(repoDataSets[i]));
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
                            CreatePlatform(repo4Image2Dockerfile, new string[0])))
                );
                File.WriteAllText(Path.Combine(context.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                string resultsContent = File.ReadAllText(command.Options.DestinationImageInfoPath);
                RepoData[] actual = JsonConvert.DeserializeObject<RepoData[]>(resultsContent);

                RepoData[] expected = new RepoData[]
                {
                    new RepoData
                    {
                        Repo = "repo1"
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
                                    new PlatformData
                                    {
                                        Path = repo2Image1Dockerfile,
                                        BaseImages = new SortedDictionary<string, string>
                                        {
                                            { "base1", "base1hash" }
                                        },
                                        SimpleTags =
                                        {
                                            "tag1",
                                            "tag3"
                                        }
                                    }
                                }
                            },
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData
                                    {
                                        Path = repo2Image2Dockerfile,
                                        SimpleTags =
                                        {
                                            "tag2"
                                        }
                                    }
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
                        Images = new List<ImageData>
                        {
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData
                                    {
                                        Path = repo4Image2Dockerfile,
                                        SimpleTags =
                                        {
                                            "tag1",
                                            "tag2"
                                        }
                                    }
                                }
                            },
                            new ImageData
                            {
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData
                                    {
                                        Path = repo4Image3Dockerfile,
                                        SimpleTags =
                                        {
                                            "tag1"
                                        }
                                    }
                                }
                            }
                        }
                    },
                };

                ImageInfoHelperTests.CompareRepos(expected, actual);
            }
        }

        [Fact]
        public async Task MergeImageInfoFilesCommand_DuplicateDockerfilePaths()
        {
            using (TempFolderContext context = TestHelper.UseTempFolder())
            {
                string dockerfile1 = CreateDockerfile("1.0/repo1/os1", context);
                string dockerfile2 = CreateDockerfile("1.0/repo1/os2", context);

                List<RepoData[]> repoDataSets = new List<RepoData[]>
                {
                    new RepoData[]
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
                                        new PlatformData
                                        {
                                            Architecture = "arm",
                                            Digest = "digest1",
                                            Path = dockerfile1,
                                            SimpleTags =
                                            {
                                                "tag1",
                                                "tag2"
                                            }
                                        },
                                        new PlatformData
                                        {
                                            Architecture = "arm64",
                                            Digest = "digest2",
                                            Path = dockerfile1,
                                            SimpleTags =
                                            {
                                                "tag2"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new RepoData[]
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
                                        new PlatformData
                                        {
                                            Architecture = "arm64",
                                            Digest = "digest2-new",
                                            Path = dockerfile1,
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
                    },
                    new RepoData[]
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
                                        new PlatformData
                                        {
                                            Digest = "digest3",
                                            Path = dockerfile2,
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

                MergeImageInfoCommand command = new MergeImageInfoCommand();
                command.Options.SourceImageInfoFolderPath = Path.Combine(context.Path, "image-infos");
                command.Options.DestinationImageInfoPath = Path.Combine(context.Path, "output.json");
                command.Options.Manifest = Path.Combine(context.Path, "manifest.json");

                Directory.CreateDirectory(command.Options.SourceImageInfoFolderPath);
                for (int i = 0; i < repoDataSets.Count; i++)
                {
                    string file = Path.Combine(command.Options.SourceImageInfoFolderPath, $"{i}.json");
                    File.WriteAllText(file, JsonHelper.SerializeObject(repoDataSets[i]));
                }

                Manifest manifest = CreateManifest(
                    CreateRepo("repo1",
                        CreateImage(
                            CreatePlatform(dockerfile1, new string[0]),
                            CreatePlatform(dockerfile1, new string[0])),
                        CreateImage(
                            CreatePlatform(dockerfile2, new string[0])))
                );
                File.WriteAllText(Path.Combine(context.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                string resultsContent = File.ReadAllText(command.Options.DestinationImageInfoPath);
                RepoData[] actual = JsonConvert.DeserializeObject<RepoData[]>(resultsContent);

                RepoData[] expected = new RepoData[]
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
                                    new PlatformData
                                    {
                                        Architecture = "arm",
                                        Digest = "digest1",
                                        Path = dockerfile1,
                                        SimpleTags =
                                        {
                                            "tag1",
                                            "tag2"
                                        }
                                    },
                                    new PlatformData
                                    {
                                        Architecture = "arm64",
                                        Digest = "digest2-new",
                                        Path = dockerfile1,
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
                                Platforms = new List<PlatformData>
                                {
                                    new PlatformData
                                    {
                                        Digest = "digest3",
                                        Path = dockerfile2,
                                        SimpleTags =
                                        {
                                            "tag1"
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                ImageInfoHelperTests.CompareRepos(expected, actual);
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
                // Store the content in a .txt file which the command should NOT be looking for.
                File.WriteAllText("image-info.txt",
                    JsonHelper.SerializeObject(new RepoData[] { new RepoData { Repo = "repo" } }));

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
