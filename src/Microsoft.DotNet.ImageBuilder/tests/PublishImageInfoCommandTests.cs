// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class PublishImageInfoCommandTests : IDisposable
    {
        private readonly List<string> foldersToDelete = new List<string>();

        public void Dispose()
        {
            foldersToDelete
                .Where(folder => Directory.Exists(folder))
                .ForEach(folder => Directory.Delete(folder, recursive: true));
        }

        /// <summary>
        /// Verifies the command will replace any existing tags or syndicated digests of a merged image.
        /// </summary>
        /// <remarks>
        /// See https://github.com/dotnet/docker-tools/pull/269
        /// </remarks>
        [Fact]
        public async Task PublishImageInfoCommand_ReplaceContent()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
                string repo2Image2DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/os", tempFolderContext);
                Manifest manifest = CreateManifest(
                    CreateRepo("repo1",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(repo1Image1DockerfilePath, new string[] { "tag1" })
                            },
                            productVersion: "1.0")),
                    CreateRepo("repo2",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(repo2Image2DockerfilePath, new string[] { "tag1" })
                            },
                            productVersion: "2.0"))
                );

                RepoData repo2;

                ImageArtifactDetails srcImageArtifactDetails = new ImageArtifactDetails
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
                                                "newtag"
                                            })
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SyndicatedDigests = new List<string>
                                        {
                                            "newdigest1",
                                            "newdigest2"
                                        }
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
                                            Helpers.ImageInfoHelper.CreatePlatform(repo2Image2DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "tag1"
                                            })
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
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                            simpleTags: new List<string>
                                            {
                                                "oldtag"
                                            })
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SyndicatedDigests = new List<string>
                                        {
                                            "olddigest1",
                                            "olddigest2"
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                GitOptions gitOptions = new GitOptions
                {
                    AuthToken = "token",
                    Repo = "PublishImageInfoCommand_ReplaceContent",
                    Branch = "testBranch",
                    Path = "imageinfo.json",
                    Email = "test@contoso.com",
                    Username = "test"
                };

                AzdoOptions azdoOptions = new AzdoOptions
                {
                    AccessToken = "azdo-token",
                    AzdoBranch = "testBranch",
                    AzdoRepo = "testRepo",
                    Organization = "azdo-org",
                    Project = "azdo-project",
                    AzdoPath = "imageinfo.json"
                };

                Mock<IRepository> repositoryMock = GetRepositoryMock();
                Mock<IGitService> gitServiceMock = GetGitServiceMock(repositoryMock.Object, gitOptions.Path, targetImageArtifactDetails);

                string actualImageArtifactDetailsContents = null;
                gitServiceMock
                    .Setup(o => o.Stage(It.IsAny<IRepository>(), It.IsAny<string>()))
                    .Callback((IRepository repo, string path) =>
                    {
                        actualImageArtifactDetailsContents = File.ReadAllText(path);
                    });

                PublishImageInfoCommand command = new PublishImageInfoCommand(gitServiceMock.Object, Mock.Of<ILoggerService>());
                command.Options.ImageInfoPath = file;
                command.Options.GitOptions = gitOptions;
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                ImageArtifactDetails expectedImageArtifactDetails = new ImageArtifactDetails
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
                                                "newtag"
                                            })
                                    },
                                    ProductVersion = "1.0",
                                    Manifest = new ManifestData
                                    {
                                        SyndicatedDigests = new List<string>
                                        {
                                            "newdigest1",
                                            "newdigest2"
                                        }
                                    }
                                }
                            }
                        },
                        repo2
                    }
                };

                Assert.Equal(JsonHelper.SerializeObject(expectedImageArtifactDetails), actualImageArtifactDetailsContents.Trim());

                VerifyMocks(repositoryMock);
            }
        }

        /// <summary>
        /// Verifies the command will remove any out-of-date content that exists within the target image info file,
        /// meaning that it has content which isn't reflected in the manifest.
        /// </summary>
        [Fact]
        public async Task PublishImageInfoCommand_RemoveOutOfDateContent()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
                string repo2Image2DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/os", tempFolderContext);
                Manifest manifest = CreateManifest(
                    CreateRepo("repo1",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(repo1Image1DockerfilePath, new string[0])
                            },
                            productVersion: "1.0")),
                    CreateRepo("repo2",
                        CreateImage(
                            new Platform[]
                            {
                                CreatePlatform(repo2Image2DockerfilePath, new string[0])
                            },
                            productVersion: "2.0"))
                );
                manifest.Registry = "mcr.microsoft.com";

                RepoData repo2;

                ImageArtifactDetails srcImageArtifactDetails = new ImageArtifactDetails
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
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath)
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
                                            Helpers.ImageInfoHelper.CreatePlatform(repo2Image2DockerfilePath)
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
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath)
                                    },
                                    ProductVersion = "1.0"
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(
                                            DockerfileHelper.CreateDockerfile("1.0/runtime2/os", tempFolderContext))
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

                GitOptions gitOptions = new GitOptions
                {
                    AuthToken = "token",
                    Repo = "PublishImageInfoCommand_RemoveOutOfDateContent",
                    Owner = "owner",
                    Path = "imageinfo.json",
                    Branch = "branch",
                    Email = "test@contoso.com",
                    Username = "test"
                };

                AzdoOptions azdoOptions = new AzdoOptions
                {
                    AccessToken = "azdo-token",
                    AzdoBranch = "testBranch",
                    AzdoRepo = "testRepo",
                    Organization = "azdo-org",
                    Project = "azdo-project",
                    AzdoPath = "imageinfo.json"
                };

                Mock<IRepository> repositoryMock = GetRepositoryMock();
                Mock<IGitService> gitServiceMock = GetGitServiceMock(repositoryMock.Object, gitOptions.Path, targetImageArtifactDetails);

                string actualImageArtifactDetailsContents = null;
                gitServiceMock
                    .Setup(o => o.Stage(It.IsAny<IRepository>(), It.IsAny<string>()))
                    .Callback((IRepository repo, string path) =>
                    {
                        actualImageArtifactDetailsContents = File.ReadAllText(path);
                    });

                PublishImageInfoCommand command = new PublishImageInfoCommand(gitServiceMock.Object, Mock.Of<ILoggerService>());
                command.Options.ImageInfoPath = file;
                command.Options.GitOptions = gitOptions;
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                ImageArtifactDetails expectedImageArtifactDetails = new ImageArtifactDetails
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
                                        Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath)
                                    },
                                    ProductVersion = "1.0"
                                }
                            }
                        },
                        repo2
                    }
                };

                Assert.Equal(JsonHelper.SerializeObject(expectedImageArtifactDetails), actualImageArtifactDetailsContents.Trim());

                VerifyMocks(repositoryMock);
            }
        }

        [Fact]
        public async Task WriteImageInfoContentToOutputPaths()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        [
                            CreatePlatform(repo1Image1DockerfilePath, ["newtag"])
                        ],
                        productVersion: "1.0"))
            );

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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath, simpleTags: [ "newtag" ])
                                },
                                ProductVersion = "1.0"
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
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath, simpleTags: [ "oldtag" ])
                                },
                                ProductVersion = "1.0"
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath, simpleTags: [ "tag" ])
                                },
                                ProductVersion = "2.0"
                            }
                        }
                    }
                }
            };

            GitOptions gitOptions = new()
            {
                AuthToken = "token",
                Repo = "myrepo",
                Branch = "testBranch",
                Path = "imageinfo.json",
                Email = "test@contoso.com",
                Username = "test"
            };

            AzdoOptions azdoOptions = new()
            {
                AccessToken = "azdo-token",
                AzdoBranch = "testBranch",
                AzdoRepo = "testRepo",
                Organization = "azdo-org",
                Project = "azdo-project",
                AzdoPath = "imageinfo.json"
            };

            Mock<IRepository> repositoryMock = GetRepositoryMock();
            Mock<IGitService> gitServiceMock = GetGitServiceMock(repositoryMock.Object, gitOptions.Path, targetImageArtifactDetails);

            string actualImageArtifactDetailsContents = null;
            gitServiceMock
                .Setup(o => o.Stage(It.IsAny<IRepository>(), It.IsAny<string>()))
                .Callback((IRepository repo, string path) =>
                {
                    actualImageArtifactDetailsContents = File.ReadAllText(path);
                });

            PublishImageInfoCommand command = new(gitServiceMock.Object, Mock.Of<ILoggerService>());
            command.Options.ImageInfoPath = file;
            command.Options.GitOptions = gitOptions;
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.OriginalImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "original-image-info.json");
            command.Options.UpdatedImageInfoOutputPath = Path.Combine(tempFolderContext.Path, "updated-image-info.json");

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            string actualOriginalImageInfoContent = File.ReadAllText(command.Options.OriginalImageInfoOutputPath);
            string actualUpdatedImageInfoContent = File.ReadAllText(command.Options.UpdatedImageInfoOutputPath);

            string expectedOriginalImageInfoContent = JsonHelper.SerializeObject(targetImageArtifactDetails);
            string expectedUpdatedImageInfoContent = JsonHelper.SerializeObject(srcImageArtifactDetails);

            Assert.Equal(expectedOriginalImageInfoContent, actualOriginalImageInfoContent);
            Assert.Equal(expectedUpdatedImageInfoContent, actualUpdatedImageInfoContent.Trim());
        }

        private static void VerifyMocks(Mock<IRepository> repositoryMock)
        {
            Mock<Network> networkMock = Mock.Get(repositoryMock.Object.Network);

            networkMock.Verify(o => o.Push(It.IsAny<Branch>(), It.IsAny<PushOptions>()));
        }

        private static Mock<IRepository> GetRepositoryMock()
        {
            List<Remote> remotes = new List<Remote>();

            Mock<RemoteCollection> remoteCollection = new Mock<RemoteCollection>();
            remoteCollection
                .Setup(o => o.GetEnumerator())
                .Returns(() => remotes.GetEnumerator());

            Mock<Network> networkMock = new Mock<Network>();
            networkMock
                .SetupGet(o => o.Remotes)
                .Returns(remoteCollection.Object);

            Mock<IRepository> repositoryMock = new Mock<IRepository>();
            repositoryMock
                .SetupGet(o => o.Network)
                .Returns(networkMock.Object);

            Mock<LibGit2Sharp.Index> indexMock = new Mock<LibGit2Sharp.Index>();

            repositoryMock
                .SetupGet(o => o.Index)
                .Returns(indexMock.Object);

            Mock<Branch> branchMock = new Mock<Branch>();

            Mock<BranchCollection> branchCollectionMock = new Mock<BranchCollection>();
            branchCollectionMock
                .Setup(o => o[It.IsAny<string>()])
                .Returns(branchMock.Object);

            repositoryMock
                .SetupGet(o => o.Branches)
                .Returns(branchCollectionMock.Object);

            Mock<Commit> commitMock = new Mock<Commit>();

            repositoryMock
                .Setup(o => o.Commit(It.IsAny<string>(), It.IsAny<Signature>(), It.IsAny<Signature>(), It.IsAny<CommitOptions>()))
                .Returns(commitMock.Object);

            return repositoryMock;
        }

        private Mock<IGitService> GetGitServiceMock(IRepository repository, string imageInfoPath, ImageArtifactDetails imageArtifactDetails)
        {
            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            gitServiceMock
                .Setup(o => o.CloneRepository(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CloneOptions>()))
                .Callback((string sourceUrl, string workdirPath, CloneOptions cloneOptions) =>
                {
                    Directory.CreateDirectory(workdirPath);
                    File.WriteAllText(Path.Combine(workdirPath, imageInfoPath), JsonHelper.SerializeObject(imageArtifactDetails));
                    foldersToDelete.Add(workdirPath);
                })
                .Returns(repository);
            return gitServiceMock;
        }
    }
}
