// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
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
                            CreatePlatform(repo1Image1DockerfilePath, new string[0]))),
                    CreateRepo("repo2",
                        CreateImage(
                            CreatePlatform(repo2Image2DockerfilePath, new string[0])))
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
                                        }
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
                                    }
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

                GitOptions gitOptions = new GitOptions
                {
                    AuthToken = "token",
                    Repo = "testRepo",
                    Branch = "testBranch",
                    Path = "imageinfo.json"
                };

                PublishImageInfoCommand command = new PublishImageInfoCommand(
                    gitHubClientFactoryMock.Object, Mock.Of<ILoggerService>(),
                    CreateHttpClientFactory(gitOptions, targetImageArtifactDetails));
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
                                    }
                                }
                            }
                        },
                        repo2
                    }
                };

                Func<GitObject[], bool> verifyGitObjects = (gitObjects) =>
                {
                    if (gitObjects.Length != 1)
                    {
                        return false;
                    }

                    return gitObjects[0].Content.Trim() == JsonHelper.SerializeObject(expectedImageArtifactDetails).Trim();
                };

                gitHubClientMock.Verify(
                    o => o.PostTreeAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.Is<GitObject[]>(gitObjects => verifyGitObjects(gitObjects))));
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
                            CreatePlatform(repo1Image1DockerfilePath, new string[0]))),
                    CreateRepo("repo2",
                        CreateImage(
                            CreatePlatform(repo2Image2DockerfilePath, new string[0])))
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
                                            Helpers.ImageInfoHelper.CreatePlatform(repo2Image2DockerfilePath)
                                        }
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
                                    }
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        Helpers.ImageInfoHelper.CreatePlatform(
                                            DockerfileHelper.CreateDockerfile("1.0/runtime2/os", tempFolderContext))
                                    }
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

                PublishImageInfoCommand command = new PublishImageInfoCommand(
                    gitHubClientFactoryMock.Object, Mock.Of<ILoggerService>(),
                    CreateHttpClientFactory(gitOptions, targetImageArtifactDetails));
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
                                    }
                                }
                            }
                        },
                        repo2
                    }
                };

                Func<GitObject[], bool> verifyGitObjects = (gitObjects) =>
                {
                    if (gitObjects.Length != 1)
                    {
                        return false;
                    }

                    return gitObjects[0].Content.Trim() == JsonHelper.SerializeObject(expectedImageArtifactDetails).Trim();
                };

                gitHubClientMock.Verify(
                    o => o.PostTreeAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.Is<GitObject[]>(gitObjects => verifyGitObjects(gitObjects))));
            }
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
                .Setup(o => o.PostTreeAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.IsAny<GitObject[]>()))
                .ReturnsAsync(new GitTree());

            GitCommit commit = new GitCommit
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
