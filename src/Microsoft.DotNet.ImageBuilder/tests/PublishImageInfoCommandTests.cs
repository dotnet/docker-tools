// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
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
            foldersToDelete.ForEach(folder => Directory.Delete(folder, recursive: true));
        }

        /// <summary>
        /// Verifies the command will replace any existing tags of a merged image.
        /// </summary>
        /// <remarks>
        /// See https://github.com/dotnet/docker-tools/pull/269
        /// </remarks>
        [Fact]
        public async Task PublishImageInfoCommand_ReplaceTags()
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
                                    ProductVersion = "1.0"
                                }
                            }
                        }
                    }
                };

                Mock<IGitHubClient> gitHubClientMock = GetGitHubClientMock();

                Mock<IGitHubClientFactory> gitHubClientFactoryMock = new Mock<IGitHubClientFactory>();
                gitHubClientFactoryMock
                    .Setup(o => o.GetClient(It.IsAny<GitHubAuth>(), false))
                    .Returns(gitHubClientMock.Object);

                AzdoOptions azdoOptions = new AzdoOptions
                {
                    AccessToken = "azdo-token",
                    Branch = "testBranch",
                    Repo = "testRepo",
                    Organization = "azdo-org",
                    Project = "azdo-project",
                    Path = "imageinfo.json"
                };

                Mock<IAzdoGitHttpClient> azdoGitHttpClientMock = GetAzdoGitHttpClient(azdoOptions);

                Mock<IAzdoGitHttpClientFactory> azdoGitHttpClientFactoryMock = new Mock<IAzdoGitHttpClientFactory>();
                azdoGitHttpClientFactoryMock
                    .Setup(o => o.GetClient(It.IsAny<Uri>(), It.IsAny<VssCredentials>()))
                    .Returns(azdoGitHttpClientMock.Object);

                GitOptions gitOptions = new GitOptions
                {
                    AuthToken = "token",
                    Repo = "testRepo",
                    Branch = "testBranch",
                    Path = "imageinfo.json"
                };

                PublishImageInfoCommand command = new PublishImageInfoCommand(
                    gitHubClientFactoryMock.Object, Mock.Of<ILoggerService>(),
                    CreateHttpClientFactory(gitOptions, targetImageArtifactDetails), azdoGitHttpClientFactoryMock.Object);
                command.Options.ImageInfoPath = file;
                command.Options.GitOptions = gitOptions;
                command.Options.AzdoOptions = azdoOptions;
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
                                    ProductVersion = "1.0"
                                }
                            }
                        },
                        repo2
                    }
                };

                VerifyMocks(gitHubClientMock, azdoGitHttpClientMock, expectedImageArtifactDetails);
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

                Mock<IGitHubClient> gitHubClientMock = GetGitHubClientMock();

                Mock<IGitHubClientFactory> gitHubClientFactoryMock = new Mock<IGitHubClientFactory>();
                gitHubClientFactoryMock
                    .Setup(o => o.GetClient(It.IsAny<GitHubAuth>(), false))
                    .Returns(gitHubClientMock.Object);

                GitOptions gitOptions = new GitOptions
                {
                    AuthToken = "token",
                    Repo = "repo",
                    Owner = "owner",
                    Path = "imageinfo.json",
                    Branch = "branch"
                };

                AzdoOptions azdoOptions = new AzdoOptions
                {
                    AccessToken = "azdo-token",
                    Branch = "testBranch",
                    Repo = "testRepo",
                    Organization = "azdo-org",
                    Project = "azdo-project",
                    Path = "imageinfo.json"
                };

                Mock<IAzdoGitHttpClient> azdoGitHttpClientMock = GetAzdoGitHttpClient(azdoOptions);

                Mock<IAzdoGitHttpClientFactory> azdoGitHttpClientFactoryMock = new Mock<IAzdoGitHttpClientFactory>();
                azdoGitHttpClientFactoryMock
                    .Setup(o => o.GetClient(It.IsAny<Uri>(), It.IsAny<VssCredentials>()))
                    .Returns(azdoGitHttpClientMock.Object);

                PublishImageInfoCommand command = new PublishImageInfoCommand(
                    gitHubClientFactoryMock.Object, Mock.Of<ILoggerService>(),
                    CreateHttpClientFactory(gitOptions, targetImageArtifactDetails), azdoGitHttpClientFactoryMock.Object);
                command.Options.ImageInfoPath = file;
                command.Options.GitOptions = gitOptions;
                command.Options.AzdoOptions = azdoOptions;
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

                VerifyMocks(gitHubClientMock, azdoGitHttpClientMock, expectedImageArtifactDetails);
            }
        }

        private static void VerifyMocks(Mock<IGitHubClient> gitHubClientMock, Mock<IAzdoGitHttpClient> azdoGitHttpClientMock, ImageArtifactDetails expectedImageArtifactDetails)
        {
            Func<VersionTools.Automation.GitHubApi.GitObject[], bool> verifyGitHubObjects = (gitHubObjects) =>
            {
                Assert.Single(gitHubObjects);
                Assert.Equal(
                    JsonHelper.SerializeObject(expectedImageArtifactDetails).Trim(),
                    gitHubObjects[0].Content.Trim());
                return true;
            };

            gitHubClientMock.Verify(
                o => o.PostTreeAsync(
                    It.IsAny<GitHubProject>(),
                    It.IsAny<string>(),
                    It.Is<VersionTools.Automation.GitHubApi.GitObject[]>(gitHubObjects => verifyGitHubObjects(gitHubObjects))));

            Func<GitPush, bool> verifyGitPush = push =>
            {
                Assert.Single(push.Commits);
                Assert.Single(push.Commits.First().Changes);
                GitChange change = push.Commits.First().Changes.First();
                Assert.Equal("imageinfo.json", change.Item.Path);
                Assert.Equal(
                    JsonHelper.SerializeObject(expectedImageArtifactDetails).Trim(),
                    Encoding.UTF8.GetString(Convert.FromBase64String(change.NewContent.Content)).Trim());

                return true;
            };
        }

        private static Mock<IAzdoGitHttpClient> GetAzdoGitHttpClient(AzdoOptions azdoOptions)
        {
            Guid repoId = Guid.NewGuid();
            const string commitId = "my-commit";

            Mock<IAzdoGitHttpClient> azdoGitHttpClientMock = new Mock<IAzdoGitHttpClient>();
            azdoGitHttpClientMock
                .Setup(o => o.GetRepositoriesAsync())
                .ReturnsAsync(new List<GitRepository>
                {
                        new GitRepository
                        {
                            Name = azdoOptions.Repo,
                            Id = repoId
                        }
                });
            azdoGitHttpClientMock
                .Setup(o => o.GetBranchRefsAsync(repoId))
                .ReturnsAsync(new List<GitRef>
                {
                        new GitRef
                        {
                            Name = $"refs/heads/{azdoOptions.Branch}"
                        }
                });
            azdoGitHttpClientMock
                .Setup(o => o.CreatePushAsync(It.IsAny<GitPush>(), repoId))
                .ReturnsAsync(new GitPush
                {
                    Commits = new GitCommitRef[]
                    {
                            new GitCommitRef
                            {
                                CommitId = commitId
                            }
                    }
                });
            azdoGitHttpClientMock
                .Setup(o => o.GetCommitAsync(commitId, repoId))
                .ReturnsAsync(new TeamFoundation.SourceControl.WebApi.GitCommit());
            return azdoGitHttpClientMock;
        }

        private static Mock<IGitHubClient> GetGitHubClientMock()
        {
            Mock<IGitHubClient> gitHubClientMock = new Mock<IGitHubClient>();

            gitHubClientMock
                .Setup(o => o.GetReferenceAsync(It.IsAny<GitHubProject>(), It.IsAny<string>()))
                .ReturnsAsync(new GitReference
                {
                    Object = new GitReferenceObject()
                });

            gitHubClientMock
                .Setup(o => o.PostTreeAsync(
                    It.IsAny<GitHubProject>(),
                    It.IsAny<string>(),
                    It.IsAny<VersionTools.Automation.GitHubApi.GitObject[]>()))
                .ReturnsAsync(new GitTree());

            VersionTools.Automation.GitHubApi.GitCommit commit = new VersionTools.Automation.GitHubApi.GitCommit
            {
                Sha = "sha"
            };

            gitHubClientMock
                .Setup(o => o.PostCommitAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
                .ReturnsAsync(commit);

            gitHubClientMock
                .Setup(o => o.PatchReferenceAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), commit.Sha, false))
                .ReturnsAsync(new GitReference
                {
                    Object = new GitReferenceObject
                    {
                        Sha = commit.Sha
                    }
                });

            return gitHubClientMock;
        }

        private IHttpClientProvider CreateHttpClientFactory(IGitFileRef imageOptionsFileRef, ImageArtifactDetails imageArtifactDetails)
        {
            string tempDir = Directory.CreateDirectory(
                    Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())).FullName;
            this.foldersToDelete.Add(tempDir);

            string repoPath = Directory.CreateDirectory(
                   Path.Combine(tempDir, $"{imageOptionsFileRef.Repo}-{imageOptionsFileRef.Branch}")).FullName;

            string imageInfoPath = Path.Combine(repoPath, imageOptionsFileRef.Path);
            File.WriteAllText(imageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

            string repoZipPath = Path.Combine(tempDir, "repo.zip");
            ZipFile.CreateFromDirectory(repoPath, repoZipPath, CompressionLevel.Fastest, true);

            Dictionary<string, HttpResponseMessage> responses = new Dictionary<string, HttpResponseMessage>
            {
                {
                    GitHelper.GetArchiveUrl(imageOptionsFileRef).ToString(),
                    new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new ByteArrayContent(File.ReadAllBytes(repoZipPath))
                    }
                }
            };

            HttpClient client = new HttpClient(new TestHttpMessageHandler(responses));

            Mock<IHttpClientProvider> httpClientFactoryMock = new Mock<IHttpClientProvider>();
            httpClientFactoryMock
                .Setup(o => o.GetClient())
                .Returns(client);

            return httpClientFactoryMock.Object;
        }
    }
}
