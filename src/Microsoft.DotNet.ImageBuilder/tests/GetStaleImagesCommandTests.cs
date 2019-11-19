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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class GetStaleImagesCommandTests
    {
        /// <summary>
        /// Verifies the correct path arguments are passed to the queued build for a basic
        /// scenario involving one image that has changed.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_SingleDigestChanged()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";
            const string dockerfile2Path = "dockerfile2/Dockerfile";

            RepoData[] imageInfoData = new RepoData[]
            {
                new RepoData
                {
                    Repo = repo1,
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            dockerfile1Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base1", "base1digest-diff" }
                                }
                            }
                        },
                        {
                            dockerfile2Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base2", "base2digest" }
                                }
                            }
                        }
                    }
                }
            };

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription(repo1)
            };

            Dictionary<Subscription, Manifest> subscriptionManifests =
                new Dictionary<Subscription, Manifest>
            {
                {
                    subscriptions[0],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo1,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                ManifestHelper.CreatePlatform(dockerfile2Path, new string[] { "tag2" }))))
                }
            };

            Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitRepo, List<DockerfileInfo>>
            {
                {
                    subscriptions[0].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, "base1", "base1digest"),
                        new DockerfileInfo(dockerfile2Path, "base2", "base2digest")
                    }
                }
            };

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // Only one of the images has a changed digest
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            dockerfile1Path
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies that a subscription will be skipped if it's associated with a different OS type than the command is assigned with.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_OsTypeFiltering_MatchingCommandFilter()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1";
            const string dockerfile2Path = "dockerfile2";
            const string dockerfile3Path = "dockerfile3";

            RepoData[] imageInfoData = new RepoData[]
            {
            };

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription(repo1, osType: "windows")
            };

            Dictionary<Subscription, Manifest> subscriptionManifests =
                new Dictionary<Subscription, Manifest>
            {
                {
                    subscriptions[0],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo1,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile1Path, new string[] { "tag1" }, OS.Windows),
                                ManifestHelper.CreatePlatform(dockerfile2Path, new string[] { "tag2" }, OS.Linux),
                                ManifestHelper.CreatePlatform(dockerfile3Path, new string[] { "tag3" }, OS.Windows))))
                }
            };

            Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitRepo, List<DockerfileInfo>>
            {
                {
                    subscriptions[0].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, "base1", "base1digest"),
                        new DockerfileInfo(dockerfile2Path, "base2", "base2digest"),
                        new DockerfileInfo(dockerfile3Path, "base3", "base3digest")
                    }
                }
            };

            // Use windows here for the command's OsType filter which is the same as the subscription's OsType.
            // This should cause the subscription to be processed.
            const string commandOsType = "windows";

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos, commandOsType))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            dockerfile1Path,
                            dockerfile3Path
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies that a subscription will be skipped if it's associated with a different OS type than the command is assigned with.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_OsTypeFiltering_NonMatchingCommandFilter()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1";
            const string dockerfile2Path = "dockerfile2";
            const string dockerfile3Path = "dockerfile3";

            RepoData[] imageInfoData = new RepoData[]
            {
            };

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription(repo1, osType: "windows")
            };

            Dictionary<Subscription, Manifest> subscriptionManifests =
                new Dictionary<Subscription, Manifest>
            {
                {
                    subscriptions[0],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo1,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile1Path, new string[] { "tag1" }, OS.Windows),
                                ManifestHelper.CreatePlatform(dockerfile2Path, new string[] { "tag2" }, OS.Linux),
                                ManifestHelper.CreatePlatform(dockerfile3Path, new string[] { "tag3" }, OS.Windows))))
                }
            };

            Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitRepo, List<DockerfileInfo>>
            {
                {
                    subscriptions[0].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, "base1", "base1digest"),
                        new DockerfileInfo(dockerfile2Path, "base2", "base2digest"),
                        new DockerfileInfo(dockerfile3Path, "base3", "base3digest")
                    }
                }
            };

            // Use linux here for the command's OsType filter which is different than the subscription's OsType of Windows.
            // This should cause the subscription to be ignored.
            const string commandOsType = "linux";

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos, commandOsType))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued build when
        /// the images have no data reflected in the image info data.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_MissingImageInfo()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";
            const string dockerfile2Path = "dockerfile2/Dockerfile";

            RepoData[] imageInfoData = new RepoData[]
            {
            };

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription(repo1)
            };

            Dictionary<Subscription, Manifest> subscriptionManifests =
                new Dictionary<Subscription, Manifest>
            {
                {
                    subscriptions[0],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo1,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                ManifestHelper.CreatePlatform(dockerfile2Path, new string[] { "tag2" }))))
                }
            };

            Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitRepo, List<DockerfileInfo>>
            {
                {
                    subscriptions[0].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, "base1", "base1digest"),
                        new DockerfileInfo(dockerfile2Path, "base2", "base2digest")
                    }
                }
            };

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // Since neither of the images existed in the image info data, both should be queued.
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            dockerfile1Path,
                            dockerfile2Path
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued builds for two
        /// subscriptions that have changed images.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_MultiSubscription()
        {
            const string repo1 = "test-repo";
            const string repo2 = "test-repo2";
            const string repo3 = "test-repo3";
            const string dockerfile1Path = "dockerfile1/Dockerfile";
            const string dockerfile2Path = "dockerfile2/Dockerfile";
            const string dockerfile3Path = "dockerfile3/Dockerfile";

            RepoData[] imageInfoData = new RepoData[]
            {
                new RepoData
                {
                    Repo = repo1,
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            dockerfile1Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base1", "base1digest-diff" }
                                }
                            }
                        }
                    }
                },
                new RepoData
                {
                    Repo = repo2,
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            dockerfile2Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base2", "base2digest-diff" }
                                }
                            }
                        }
                    }
                },
                new RepoData
                {
                    Repo = repo3,
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            dockerfile3Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base3", "base3digest" }
                                }
                            }
                        }
                    }
                }
            };

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription(repo1, 1),
                CreateSubscription(repo2, 2)
            };

            Dictionary<Subscription, Manifest> subscriptionManifests =
                new Dictionary<Subscription, Manifest>
            {
                {
                    subscriptions[0],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo1,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile1Path, new string[] { "tag1" }))))
                },
                {
                    subscriptions[1],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo2,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile2Path, new string[] { "tag2" }))),
                        ManifestHelper.CreateRepo(
                            repo3,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile3Path, new string[] { "tag3" }))))
                }
            };

            Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitRepo, List<DockerfileInfo>>
            {
                {
                    subscriptions[0].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, "base1", "base1digest")
                    }
                },
                {
                    subscriptions[1].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile2Path, "base2", "base2digest"),
                        new DockerfileInfo(dockerfile3Path, "base3", "base3digest")
                    }
                }
            };

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            dockerfile1Path
                        }
                    },
                    {
                        subscriptions[1],
                        new List<string>
                        {
                            dockerfile2Path
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies that a base image's digest will be cached and not pulled for a subsequent image.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_BaseImageCaching()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";
            const string dockerfile2Path = "dockerfile2/Dockerfile";
            const string baseImage = "base1";
            const string baseImageDigest = "base1digest";

            RepoData[] imageInfoData = new RepoData[]
            {
                new RepoData
                {
                    Repo = repo1,
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            dockerfile1Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { baseImage, baseImageDigest + "-diff" }
                                }
                            }
                        },
                        {
                            dockerfile2Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { baseImage, baseImageDigest + "-diff" }
                                }
                            }
                        }
                    }
                }
            };

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription(repo1)
            };

            Dictionary<Subscription, Manifest> subscriptionManifests =
                new Dictionary<Subscription, Manifest>
            {
                {
                    subscriptions[0],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo1,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                ManifestHelper.CreatePlatform(dockerfile2Path, new string[] { "tag2" }))))
                }
            };

            Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitRepo, List<DockerfileInfo>>
            {
                {
                    subscriptions[0].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, baseImage, baseImageDigest),
                        new DockerfileInfo(dockerfile2Path, baseImage, baseImageDigest)
                    }
                }
            };

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // Both of the images has a changed digest
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            dockerfile1Path,
                            dockerfile2Path
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);

                // Verify the image was pulled only once
                context.DockerServiceMock
                    .Verify(o => o.PullImage(baseImage, false), Times.Once);
                context.DockerServiceMock
                    .Verify(o => o.GetImageDigest(baseImage, false), Times.Once);
            }
        }

        /// <summary>
        /// Verifies that no build will be queued if the base image has not changed.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_NoBaseImageChange()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";
            const string baseImage = "base1";
            const string baseImageDigest = "base1digest";

            RepoData[] imageInfoData = new RepoData[]
            {
                new RepoData
                {
                    Repo = repo1,
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            dockerfile1Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { baseImage, baseImageDigest }
                                }
                            }
                        }
                    }
                }
            };

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription(repo1)
            };

            Dictionary<Subscription, Manifest> subscriptionManifests =
                new Dictionary<Subscription, Manifest>
            {
                {
                    subscriptions[0],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo1,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile1Path, new string[] { "tag1" }))))
                }
            };

            Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitRepo, List<DockerfileInfo>>
            {
                {
                    subscriptions[0].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, baseImage, baseImageDigest)
                    }
                }
            };

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // No paths are expected
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>();

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued build when
        /// a base image changes where the image referencing that base image has other
        /// images dependent upon it.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_DependencyGraph()
        {
            const string runtimeDepsRepo = "runtimedeps-repo";
            const string runtimeRepo = "runtime-repo";
            const string sdkRepo = "sdk-repo";
            const string aspnetRepo = "aspnet-repo";
            const string otherRepo = "other-repo";
            const string runtimeDepsDockerfilePath = "runtime-deps/Dockerfile";
            const string runtimeDockerfilePath = "runtime/Dockerfile";
            const string sdkDockerfilePath = "sdk/Dockerfile";
            const string aspnetDockerfilePath = "aspnet/Dockerfile";
            const string otherDockerfilePath = "other/Dockerfile";
            const string baseImage = "base1";
            const string baseImageDigest = "base1digest";
            const string otherImage = "other";
            const string otherImageDigest = "otherDigest";

            RepoData[] imageInfoData = new RepoData[]
            {
                new RepoData
                {
                    Repo = runtimeDepsRepo,
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            runtimeDepsDockerfilePath,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { baseImage, baseImageDigest + "-diff" }
                                }
                            }
                        }
                    }
                },
                new RepoData
                {
                    Repo = runtimeRepo
                },
                new RepoData
                {
                    Repo = sdkRepo
                },
                new RepoData
                {
                    Repo = aspnetRepo
                },
                // Include an image that has not been changed and should not be included in the expected paths.
                new RepoData
                {
                    Repo = otherRepo,
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            otherDockerfilePath,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { otherImage, otherImageDigest }
                                }
                            }
                        }
                    }
                }
            };

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription("repo1")
            };

            Dictionary<Subscription, Manifest> subscriptionManifests =
                new Dictionary<Subscription, Manifest>
            {
                {
                    subscriptions[0],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            runtimeDepsRepo,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(runtimeDepsDockerfilePath, new string[] { "tag1" }))),
                        ManifestHelper.CreateRepo(
                            runtimeRepo,
                            ManifestHelper.CreateImage(
                                CreatePlatformWithRepoBuildArg(runtimeDockerfilePath, "runtime-deps", new string[] { "tag1" }))),
                        ManifestHelper.CreateRepo(
                            sdkRepo,
                            ManifestHelper.CreateImage(
                                CreatePlatformWithRepoBuildArg(sdkDockerfilePath, "runtime", new string[] { "tag1" }))),
                        ManifestHelper.CreateRepo(
                            aspnetRepo,
                            ManifestHelper.CreateImage(
                                CreatePlatformWithRepoBuildArg(aspnetDockerfilePath, "runtime", new string[] { "tag1" }))),
                        ManifestHelper.CreateRepo(
                            otherRepo,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(otherDockerfilePath, new string[] { "tag1" }))))
                }
            };

            Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitRepo, List<DockerfileInfo>>
            {
                {
                    subscriptions[0].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(runtimeDepsDockerfilePath, baseImage, baseImageDigest),
                        new DockerfileInfo(runtimeDockerfilePath, null, null, hasInternalFrom: true),
                        new DockerfileInfo(sdkDockerfilePath, null, null, hasInternalFrom: true),
                        new DockerfileInfo(aspnetDockerfilePath, null, null, hasInternalFrom: true),
                        new DockerfileInfo(otherDockerfilePath, otherImage, otherImageDigest)
                    }
                }
            };

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            runtimeDepsDockerfilePath,
                            runtimeDockerfilePath,
                            sdkDockerfilePath,
                            aspnetDockerfilePath
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued build when an image
        /// built from a custom named Dockerfile.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_CustomDockerfilePath()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "path/to/Dockerfile.custom";
            const string dockerfile2Path = "path/to/Dockerfile";

            RepoData[] imageInfoData = new RepoData[]
            {
                new RepoData
                {
                    Repo = repo1,
                    Images = new SortedDictionary<string, ImageData>
                    {
                        {
                            dockerfile1Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base1", "base1digest-diff" }
                                }
                            }
                        },
                        {
                            dockerfile2Path,
                            new ImageData
                            {
                                BaseImages = new SortedDictionary<string, string>
                                {
                                    { "base2", "base2digest" }
                                }
                            }
                        }
                    }
                }
            };

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription(repo1)
            };

            Dictionary<Subscription, Manifest> subscriptionManifests =
                new Dictionary<Subscription, Manifest>
            {
                {
                    subscriptions[0],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo1,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                ManifestHelper.CreatePlatform(dockerfile2Path, new string[] { "tag2" }))))
                }
            };

            Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitRepo, List<DockerfileInfo>>
            {
                {
                    subscriptions[0].RepoInfo,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, "base1", "base1digest"),
                        new DockerfileInfo(dockerfile2Path, "base2", "base2digest")
                    }
                }
            };

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // Only one of the images has a changed digest
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            dockerfile1Path
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Use this method to generate a unique repo owner name for the tests. This ensures that each test
        /// uses a different name and prevents collisions when running the tests in parallel. This is because
        /// the <see cref="GetStaleImagesCommand"/> generates temp folders partially based on the name of
        /// the repo owner.
        /// </summary>
        private static string GetRepoOwner([CallerMemberName] string testMethodName = null, string suffix = null)
        {
            return testMethodName + suffix;
        }

        private static Platform CreatePlatformWithRepoBuildArg(string dockerfilePath, string repo, string[] tags, OS os = OS.Linux)
        {
            Platform platform = ManifestHelper.CreatePlatform(dockerfilePath, tags, os);
            platform.BuildArgs = new Dictionary<string, string>
            {
                { "REPO", repo }
            };
            return platform;
        }

        private static Subscription CreateSubscription(
            string repoName,
            int index = 0,
            string osType = null,
            [CallerMemberName] string testMethodName = null)
        {
            return new Subscription
            {
                ManifestPath = "testmanifest.json",
                PipelineTrigger = new PipelineTrigger
                {
                    Id = 1,
                    PathVariable = "--my-path"
                },
                RepoInfo = new GitRepo
                {
                    Branch = "testBranch" + index,
                    Name = repoName,
                    Owner = GetRepoOwner(testMethodName, index.ToString())
                },
                OsType = osType
            };
        }

        /// <summary>
        /// Sets up the test state from the provided metadata, executes the test, and verifies the results.
        /// </summary>
        private class TestContext : IDisposable
        {
            private readonly List<string> filesToCleanup = new List<string>();
            private readonly List<string> foldersToCleanup = new List<string>();
            private readonly Dictionary<string, string> imageDigests = new Dictionary<string, string>();
            private readonly string subscriptionsPath;
            private readonly IHttpClientFactory httpClientFactory;
            private readonly GetStaleImagesCommand command;
            private readonly Mock<ILoggerService> loggerServiceMock = new Mock<ILoggerService>();
            private readonly string osType;
            private readonly IGitHubClientFactory gitHubClientFactory;

            private const string VariableName = "my-var";
            private const string GitHubBranch = "my-branch";
            private const string GitHubRepo = "my-repo";
            private const string GitHubOwner = "my-owner";
            private const string GitHubPath = "my-path";

            public Mock<IDockerService> DockerServiceMock { get; }

            /// <summary>
            /// Initializes a new instance of <see cref="TestContext"/>.
            /// </summary>
            /// <param name="imageInfoData">The set of image info data for all Git repos.</param>
            /// <param name="subscriptions">The set of subscription metadata describing the Git repos that are listening for changes to base images.</param>
            /// <param name="subscriptionManifests">A mapping of subscriptions to their associated manifests.</param>
            /// <param name="dockerfileInfos">A mapping of Git repos to their associated set of Dockerfiles.</param>
            /// <param name="osType">The OS type to filter the command with.</param>
            public TestContext(
                RepoData[] imageInfoData,
                Subscription[] subscriptions,
                IDictionary<Subscription, Manifest> subscriptionManifests,
                Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos,
                string osType = "*")
            {
                this.osType = osType;
                this.subscriptionsPath = this.SerializeJsonObjectToTempFile(subscriptions);

                // Cache image digests lookup
                foreach (DockerfileInfo dockerfileInfo in dockerfileInfos.Values.SelectMany(info => info))
                {
                    if (dockerfileInfo.ImageName != null)
                    {
                        this.imageDigests[dockerfileInfo.ImageName] = dockerfileInfo.ImageDigest;
                    }
                }

                TeamProject project = new TeamProject
                {
                    Id = Guid.NewGuid()
                };

                this.httpClientFactory = CreateHttpClientFactory(subscriptions, subscriptionManifests, dockerfileInfos);

                string imageInfoContents = JsonConvert.SerializeObject(imageInfoData);
                this.gitHubClientFactory = CreateGitHubClientFactory(imageInfoContents);

                this.DockerServiceMock = this.CreateDockerServiceMock();
                this.command = this.CreateCommand();
            }

            public Task ExecuteCommandAsync()
            {
                return this.command.ExecuteAsync();
            }

            /// <summary>
            /// Verifies the test execution to ensure the results match the expected state.
            /// </summary>
            public void Verify(IDictionary<Subscription, IList<string>> expectedPathsBySubscription)
            {
                IInvocation invocation = this.loggerServiceMock.Invocations
                    .First(invocation => invocation.Method.Name == nameof(ILoggerService.WriteMessage) &&
                        invocation.Arguments[0].ToString().StartsWith("##vso"));
                
                string message = invocation.Arguments[0].ToString();
                int variableNameStartIndex = message.IndexOf("=") + 1;
                string actualVariableName = message.Substring(variableNameStartIndex, message.IndexOf(";") - variableNameStartIndex);
                Assert.Equal(VariableName, actualVariableName);

                string variableValue = message
                    .Substring(message.IndexOf("]") + 1);

                SubscriptionImagePaths[] pathsBySubscription = 
                    JsonConvert.DeserializeObject<SubscriptionImagePaths[]>(variableValue.Replace("\\\"", "\""));

                Assert.Equal(expectedPathsBySubscription.Count, pathsBySubscription.Length);

                foreach (KeyValuePair<Subscription, IList<string>> kvp in expectedPathsBySubscription)
                {
                    string[] actualPaths = pathsBySubscription
                        .First(imagePaths => imagePaths.SubscriptionId == kvp.Key.Id).ImagePaths;

                    Assert.Equal(kvp.Value, actualPaths);
                }
            }

            private string SerializeJsonObjectToTempFile(object jsonObject)
            {
                string path = Path.GetTempFileName();
                File.WriteAllText(path, JsonConvert.SerializeObject(jsonObject));
                this.filesToCleanup.Add(path);
                return path;
            }

            private GetStaleImagesCommand CreateCommand()
            {
                GetStaleImagesCommand command = new GetStaleImagesCommand(
                    this.DockerServiceMock.Object, this.httpClientFactory, this.loggerServiceMock.Object, this.gitHubClientFactory);
                command.Options.SubscriptionsPath = this.subscriptionsPath;
                command.Options.VariableName = VariableName;
                command.Options.FilterOptions.OsType = this.osType;
                command.Options.GitOptions.Branch = GitHubBranch;
                command.Options.GitOptions.Owner = GitHubOwner;
                command.Options.GitOptions.Repo = GitHubRepo;
                command.Options.GitOptions.Path = GitHubPath;
                command.Options.GitOptions.Email = "test";
                command.Options.GitOptions.Username = "test";
                command.Options.GitOptions.AuthToken = "test";
                return command;
            }

            private IGitHubClientFactory CreateGitHubClientFactory(string imageInfoContents)
            {
                Mock<IGitHubClient> gitHubClientMock = new Mock<IGitHubClient>();
                gitHubClientMock
                    .Setup(o => o.GetGitHubFileContentsAsync(It.IsAny<string>(), It.Is<GitHubBranch>(branch => IsMatchingBranch(branch))))
                    .ReturnsAsync(imageInfoContents);

                Mock<IGitHubClientFactory> gitHubClientFactoryMock = new Mock<IGitHubClientFactory>();
                gitHubClientFactoryMock
                    .Setup(o => o.GetClient(It.IsAny<GitHubAuth>()))
                    .Returns(gitHubClientMock.Object);

                return gitHubClientFactoryMock.Object;
            }

            private static bool IsMatchingBranch(GitHubBranch branch)
            {
                return branch.Name == GitHubBranch &&
                    branch.Project.Name == GitHubRepo &&
                    branch.Project.Owner == GitHubOwner;
            }

            /// <summary>
            /// Returns an <see cref="IHttpClientFactory"/> that creates an <see cref="HttpClient"/> which 
            /// bypasses the network and return back pre-built responses for GitHub repo zip files.
            /// </summary>
            /// <param name="subscriptions">The set of subscriptions referring to GitHub repos that should have file responses.</param>
            /// <param name="subscriptionManifests">A mapping of subscriptions to their associated manifests.</param>
            /// <param name="dockerfileInfos">A mapping of Git repos to their associated set of Dockerfiles.</param>
            private IHttpClientFactory CreateHttpClientFactory(
                Subscription[] subscriptions,
                IDictionary<Subscription, Manifest> subscriptionManifests,
                Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos)
            {
                Dictionary<string, HttpResponseMessage> responses = new Dictionary<string, HttpResponseMessage>();
                foreach (Subscription subscription in subscriptions)
                {
                    if (subscriptionManifests.TryGetValue(subscription, out Manifest manifest))
                    {
                        List<DockerfileInfo> repoDockerfileInfos = dockerfileInfos[subscription.RepoInfo];
                        string repoZipPath = GenerateRepoZipFile(subscription, manifest, repoDockerfileInfos);

                        responses.Add(
                            $"https://github.com/{subscription.RepoInfo.Owner}/{subscription.RepoInfo.Name}/archive/{subscription.RepoInfo.Branch}.zip",
                            new HttpResponseMessage
                            {
                                StatusCode = HttpStatusCode.OK,
                                Content = new ByteArrayContent(File.ReadAllBytes(repoZipPath))
                            });
                    }
                }

                HttpClient client = new HttpClient(new TestHttpMessageHandler(responses));

                Mock<IHttpClientFactory> httpClientFactoryMock = new Mock<IHttpClientFactory>();
                httpClientFactoryMock
                    .Setup(o => o.GetClient())
                    .Returns(client);

                return httpClientFactoryMock.Object;
            }

            /// <summary>
            /// Generates a zip file in a temp location that represents the contents of a GitHub repo.
            /// </summary>
            /// <param name="subscription">The subscription associated with the GitHub repo.</param>
            /// <param name="manifest">Manifest model associated with the subscription.</param>
            /// <param name="repoDockerfileInfos">Set of <see cref="DockerfileInfo"/> objects that describe the Dockerfiles contained in the repo.</param>
            /// <returns></returns>
            private string GenerateRepoZipFile(
                Subscription subscription,
                Manifest manifest,
                List<DockerfileInfo> repoDockerfileInfos)
            {
                // Create a temp folder to store everything in.
                string tempDir = Directory.CreateDirectory(
                    Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())).FullName;
                this.foldersToCleanup.Add(tempDir);

                // Create a sub-folder inside the temp folder that represents the repo contents.
                string repoPath = Directory.CreateDirectory(
                    Path.Combine(tempDir, $"{subscription.RepoInfo.Name}-{subscription.RepoInfo.Branch}")).FullName;

                // Serialize the manifest model to a file in the repo folder.
                string manifestPath = Path.Combine(repoPath, subscription.ManifestPath);
                File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

                foreach (DockerfileInfo dockerfileInfo in repoDockerfileInfos)
                {
                    GenerateDockerfile(dockerfileInfo, repoPath);
                }

                string repoZipPath = Path.Combine(tempDir, "repo.zip");
                ZipFile.CreateFromDirectory(repoPath, repoZipPath, CompressionLevel.Fastest, true);
                return repoZipPath;
            }

            /// <summary>
            /// Generates a Dockerfile from the <see cref="DockerfileInfo"/> metatada and stores it the specified path.
            /// </summary>
            /// <param name="dockerfileInfo">The metadata for the Dockerfile.</param>
            /// <param name="destinationPath">Folder path to store the Dockerfile.</param>
            private static void GenerateDockerfile(DockerfileInfo dockerfileInfo, string destinationPath)
            {
                string dockerfileContents;
                if (dockerfileInfo.HasInternalFrom)
                {
                    dockerfileContents = $"FROM $REPO:{dockerfileInfo.ImageName}";
                }
                else
                {
                    dockerfileContents = $"FROM {dockerfileInfo.ImageName}";
                }

                string dockerfilePath = Directory.CreateDirectory(
                    Path.Combine(destinationPath, Path.GetDirectoryName(dockerfileInfo.DockerfilePath))).FullName;

                File.WriteAllText(Path.Combine(dockerfilePath, Path.GetFileName(dockerfileInfo.DockerfilePath)), dockerfileContents);
            }

            private Mock<IDockerService> CreateDockerServiceMock()
            {
                Mock<IDockerService> dockerServiceMock = new Mock<IDockerService>();
                dockerServiceMock
                    .Setup(o => o.GetImageDigest(It.IsAny<string>(), false))
                    .Returns((string image, bool isDryRun) => this.imageDigests[image]);
                return dockerServiceMock;
            }

            /// <summary>
            /// Returns a value indicating whether the <see cref="Build"/> object contains the expected state.
            /// </summary>
            /// <param name="build">The <see cref="Build"/> to validate.</param>
            /// <param name="subscription">Subscription object that contains metadata to compare against the <paramref name="build"/>.</param>
            /// <param name="expectedPaths">The set of expected path arguments that should have been passed to the build.</param>
            private static bool FilterBuildToSubscription(Build build, Subscription subscription, IList<string> expectedPaths)
            {
                return build.Definition.Id == subscription.PipelineTrigger.Id &&
                    build.SourceBranch == subscription.RepoInfo.Branch &&
                    FilterBuildToParameters(build.Parameters, subscription.PipelineTrigger.PathVariable, expectedPaths);
            }

            /// <summary>
            /// Returns a value indicating whether <paramref name="buildParametersJson"/> matches the expected results.
            /// </summary>
            /// <param name="buildParametersJson">The raw JSON parameters value that was provided to a <see cref="Build"/>.</param>
            /// <param name="pathVariable">Name of the path variable that the arguments are assigned to.</param>
            /// <param name="expectedPaths">The set of expected path arguments that should have been passed to the build.</param>
            private static bool FilterBuildToParameters(string buildParametersJson, string pathVariable, IList<string> expectedPaths)
            {
                JObject buildParameters = JsonConvert.DeserializeObject<JObject>(buildParametersJson);
                string pathString = buildParameters[pathVariable].ToString();
                IList<string> paths = pathString
                    .Split("--path ", StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim().Trim('\''))
                    .ToList();
                return TestHelper.CompareLists(expectedPaths, paths);
            }

            public void Dispose()
            {
                foreach (string file in this.filesToCleanup)
                {
                    File.Delete(file);
                }

                foreach (string folder in this.foldersToCleanup)
                {
                    Directory.Delete(folder, true);
                }

                this.command?.Dispose();
            }
        }

        private class DockerfileInfo
        {
            public DockerfileInfo(string dockerfilePath, string imageName, string imageDigest, bool hasInternalFrom = false)
            {
                this.DockerfilePath = dockerfilePath;
                this.ImageName = imageName;
                this.ImageDigest = imageDigest;
                this.HasInternalFrom = hasInternalFrom;
            }

            public string DockerfilePath { get; }
            public string ImageName { get; }
            public string ImageDigest { get; }
            public bool HasInternalFrom { get; }
        }

        private class PagedList<T> : List<T>, IPagedList<T>
        {
            public string ContinuationToken => throw new NotImplementedException();
        }
    }
}
