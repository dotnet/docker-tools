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
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class PublishImageInfoCommandTests
    {
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
                RepoData repo2;

                RepoData[] sourceRepos = new RepoData[]
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
                                        Path = "image1",
                                        SimpleTags =
                                        {
                                            "newtag"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    repo2 = new RepoData
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
                                        Path = "image2",
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

                string file = Path.Combine(tempFolderContext.Path, "image-info.json");
                File.WriteAllText(file, JsonHelper.SerializeObject(sourceRepos));

                RepoData[] targetRepos = new RepoData[]
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
                                        Path = "image1",
                                        SimpleTags =
                                        {
                                            "oldtag"
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                Mock<IGitHubClient> gitHubClientMock = GetGitHubClientMock(targetRepos);

                Mock<IGitHubClientFactory> gitHubClientFactoryMock = new Mock<IGitHubClientFactory>();
                gitHubClientFactoryMock
                    .Setup(o => o.GetClient(It.IsAny<GitHubAuth>(), false))
                    .Returns(gitHubClientMock.Object);

                PublishImageInfoCommand command = new PublishImageInfoCommand(gitHubClientFactoryMock.Object);
                command.Options.ImageInfoPath = file;
                command.Options.GitOptions.AuthToken = "token";
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");

                Manifest manifest = ManifestHelper.CreateManifest();
                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                command.LoadManifest();
                await command.ExecuteAsync();

                RepoData[] expectedRepos = new RepoData[]
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
                                        Path = "image1",
                                        SimpleTags =
                                        {
                                            "newtag"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    repo2
                };

                Func<GitObject[], bool> verifyGitObjects = (gitObjects) =>
                {
                    if (gitObjects.Length != 1)
                    {
                        return false;
                    }

                    return gitObjects[0].Content.Trim() == JsonHelper.SerializeObject(expectedRepos).Trim();
                };

                gitHubClientMock.Verify(
                    o => o.PostTreeAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.Is<GitObject[]>(gitObjects => verifyGitObjects(gitObjects))));
            }
        }

        private static Mock<IGitHubClient> GetGitHubClientMock(RepoData[] targetRepos)
        {
            Mock<IGitHubClient> gitHubClientMock = new Mock<IGitHubClient>();
            gitHubClientMock
                .Setup(o => o.GetGitHubFileContentsAsync(It.IsAny<string>(), It.IsAny<GitHubBranch>()))
                .ReturnsAsync(JsonHelper.SerializeObject(targetRepos));

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
    }
}
