﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;
using WebApi = Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class GetStaleImagesCommandTests
    {
        private const string GitHubBranch = "my-branch";
        private const string GitHubRepo = "my-repo";
        private const string GitHubOwner = "my-owner";

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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: "base1@base1digest-diff",
                                                simpleTags: new List<string> { "tag1" }),
                                            CreatePlatform(
                                                dockerfile2Path,
                                                baseImageDigest: "base2@base2digest",
                                                simpleTags: new List<string> { "tag2" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base1", "base1digest")),
                        new DockerfileInfo(dockerfile2Path, new FromImageInfo("base2", "base2digest"))
                    }
                }
            };

            using (TestContext context = new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // Only one of the images has a changed digest
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
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
        /// Verifies the correct path arguments are passed to the queued build for a multi-stage
        /// Dockerfile scenario involving one image that has changed.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_MultiStage_SingleDigestChanged()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";
            const string dockerfile2Path = "dockerfile2/Dockerfile";

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: "base2@base2digest-diff",
                                                simpleTags: new List<string> { "tag1" }),
                                            CreatePlatform(
                                                dockerfile2Path,
                                                baseImageDigest: "base3@base3digest",
                                                simpleTags: new List<string> { "tag2" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(
                            dockerfile1Path,
                            new FromImageInfo("base1", "base1digest"),
                            new FromImageInfo("base2", "base2digest")),
                        new DockerfileInfo(
                            dockerfile2Path,
                            new FromImageInfo("base2", "base2digest"),
                            new FromImageInfo("base3", "base3digest"))
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // The base image of the final stage has changed for only one of the images.
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1, osType: "windows"),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" }, OS.Windows),
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" }, OS.Linux),
                                CreatePlatform(dockerfile3Path, new string[] { "tag3" }, OS.Windows)))),
                    new ImageArtifactDetails()
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base1", "base1digest")),
                        new DockerfileInfo(dockerfile2Path, new FromImageInfo("base2", "base2digest")),
                        new DockerfileInfo(dockerfile3Path, new FromImageInfo("base3", "base3digest"))
                    }
                }
            };

            // Use windows here for the command's OsType filter which is the same as the subscription's OsType.
            // This should cause the subscription to be processed.
            const string commandOsType = "windows";

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos, commandOsType))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1, osType: "windows"),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" }, OS.Windows),
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" }, OS.Linux),
                                CreatePlatform(dockerfile3Path, new string[] { "tag3" }, OS.Windows)))),
                    new ImageArtifactDetails()
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base1", "base1digest")),
                        new DockerfileInfo(dockerfile2Path, new FromImageInfo("base2", "base2digest")),
                        new DockerfileInfo(dockerfile3Path, new FromImageInfo("base3", "base3digest"))
                    }
                }
            };

            // Use linux here for the command's OsType filter which is different than the subscription's OsType of Windows.
            // This should cause the subscription to be ignored.
            const string commandOsType = "linux";

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos, commandOsType))
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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" })))),
                    new ImageArtifactDetails()
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base1", "base1digest")),
                        new DockerfileInfo(dockerfile2Path, new FromImageInfo("base2", "base2digest"))
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // Since neither of the images existed in the image info data, both should be queued.
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1, 1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: "base1@base1digest-diff",
                                                simpleTags: new List<string> { "tag1" })
                                        }
                                    }
                                }
                            },
                            new RepoData
                            {
                                Repo = repo2,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile2Path,
                                                baseImageDigest: "base2@base2digest-diff")
                                        }
                                    }
                                }
                            }
                        }
                    }
                ),
                new SubscriptionInfo(
                    CreateSubscription(repo2, 2),
                    CreateManifest(
                        CreateRepo(
                            repo2,
                            CreateImage(
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" }))),
                        CreateRepo(
                            repo3,
                            CreateImage(
                                CreatePlatform(dockerfile3Path, new string[] { "tag3" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: "base1@base1digest-diff",
                                                simpleTags: new List<string> { "tag1" })
                                        }
                                    }
                                }
                            },
                            new RepoData
                            {
                                Repo = repo2,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile2Path,
                                                baseImageDigest: "base2@base2digest-diff",
                                                simpleTags: new List<string> { "tag2" })
                                        }
                                    }
                                }
                            },
                            new RepoData
                            {
                                Repo = repo3,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile3Path,
                                                baseImageDigest: "base3@base3digest",
                                                simpleTags: new List<string> { "tag3" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base1", "base1digest"))
                    }
                },
                {
                    subscriptionInfos[1].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile2Path, new FromImageInfo("base2", "base2digest")),
                        new DockerfileInfo(dockerfile3Path, new FromImageInfo("base3", "base3digest"))
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
                        new List<string>
                        {
                            dockerfile1Path
                        }
                    },
                    {
                        subscriptionInfos[1].Subscription,
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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images = new List<ImageData>
                                {
                                    new ImageData
                                    {
                                        Platforms = new List<PlatformData>
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: baseImageDigest + "-diff",
                                                simpleTags: new List<string> { "tag1" }),
                                            CreatePlatform(
                                                dockerfile2Path,
                                                baseImageDigest: baseImageDigest + "-diff",
                                                simpleTags: new List<string> { "tag2" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };


            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo(baseImage, baseImageDigest)),
                        new DockerfileInfo(dockerfile2Path, new FromImageInfo(baseImage, baseImageDigest))
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // Both of the images has a changed digest
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
                        new List<string>
                        {
                            dockerfile1Path,
                            dockerfile2Path
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);

                context.ManifestServiceMock
                    .Verify(o => o.GetManifestAsync(baseImage, false), Times.Once);
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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: $"{baseImage}@{baseImageDigest}",
                                                simpleTags: new List<string> { "tag1" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo(baseImage, baseImageDigest))
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // No paths are expected
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>();

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies that no build will be queued for a Dockerfile with no base image.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_NoBaseImage()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                simpleTags: new List<string> { "tag1" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("scratch", null))
                    }
                }
            };

            using (TestContext context = new(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // No paths are expected
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription = new();

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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription("repo1"),
                    CreateManifest(
                        CreateRepo(
                            runtimeDepsRepo,
                            CreateImage(
                                CreatePlatform(runtimeDepsDockerfilePath, new string[] { "tag1" }))),
                        CreateRepo(
                            runtimeRepo,
                            CreateImage(
                                CreatePlatform(runtimeDockerfilePath, new string[] { "tag1" }))),
                        CreateRepo(
                            sdkRepo,
                            CreateImage(
                                CreatePlatform(sdkDockerfilePath, new string[] { "tag1" }))),
                        CreateRepo(
                            aspnetRepo,
                            CreateImage(
                                CreatePlatform(aspnetDockerfilePath, new string[] { "tag1" }))),
                        CreateRepo(
                            otherRepo,
                            CreateImage(
                                CreatePlatform(otherDockerfilePath, new string[] { "tag1" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = runtimeDepsRepo,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                runtimeDepsDockerfilePath,
                                                baseImageDigest: $"{baseImage}@{baseImageDigest}-diff",
                                                simpleTags: new List<string> { "tag1" })
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
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                otherDockerfilePath,
                                                baseImageDigest: $"{otherImage}@{otherImageDigest}",
                                                simpleTags: new List<string> { "tag1" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(runtimeDepsDockerfilePath, new FromImageInfo(baseImage, baseImageDigest)),
                        new DockerfileInfo(runtimeDockerfilePath, new FromImageInfo($"{runtimeDepsRepo}:tag1", null)),
                        new DockerfileInfo(sdkDockerfilePath, new FromImageInfo($"{aspnetRepo}:tag1", null)),
                        new DockerfileInfo(aspnetDockerfilePath, new FromImageInfo($"{runtimeRepo}:tag1", null)),
                        new DockerfileInfo(otherDockerfilePath, new FromImageInfo(otherImage, otherImageDigest))
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
                        new List<string>
                        {
                            runtimeDepsDockerfilePath,
                            runtimeDockerfilePath,
                            aspnetDockerfilePath,
                            sdkDockerfilePath,
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued build when
        /// a base image changes where the image referencing that base image has other
        /// images dependent upon it. And one of those dependent images is based another
        /// image because of a multi-stage Dockerfile. So there are two root images and
        /// both need to be included in the output. In this test, the Monitor Dockerfile
        /// has a dependency on both sdk:jammy and aspnet:jammy-chiseled which come from
        /// different roots.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_DependencyGraph_TwoRoots()
        {
            const string RuntimeDepsRepo = "runtime-deps";
            const string RuntimeRepo = "runtime";
            const string AspnetRepo = "aspnet";
            const string SdkRepo = "sdk";
            const string MonitorRepo = "monitor";

            const string JammyRuntimeDepsDockerfilePath = "runtime-deps/jammy/Dockerfile";
            const string JammyRuntimeDockerfilePath = "runtime/jammy/Dockerfile";
            const string JammyAspnetDockerfilePath = "aspnet/jammy/Dockerfile";
            const string JammySdkDockerfilePath = "sdk/jammy/Dockerfile";
            const string JammyChiseledRuntimeDepsDockerfilePath = "runtime-deps/jammy-chiseled/Dockerfile";
            const string JammyChiseledRuntimeDockerfilePath = "runtime/jammy-chiseled/Dockerfile";
            const string JammyChiseledAspnetDockerfilePath = "aspnet/jammy-chiseled/Dockerfile";
            const string JammyChiseledMonitorDockerfilePath = "monitor/jammy-chiseled/Dockerfile";

            const string baseImage = "base1";
            const string baseImageDigest = "base1digest";

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription("repo1"),
                    CreateManifest(
                        CreateRepo(
                            RuntimeDepsRepo,
                            CreateImage(
                                CreatePlatform(JammyRuntimeDepsDockerfilePath, new string[] { "jammy" })),
                            CreateImage(
                                CreatePlatform(JammyChiseledRuntimeDepsDockerfilePath, new string[] { "jammy-chiseled" }))),
                        CreateRepo(
                            RuntimeRepo,
                            CreateImage(
                                CreatePlatform(JammyRuntimeDockerfilePath, new string[] { "jammy" })),
                            CreateImage(
                                CreatePlatform(JammyChiseledRuntimeDockerfilePath, new string[] { "jammy-chiseled" }))),
                        CreateRepo(
                            AspnetRepo,
                            CreateImage(
                                CreatePlatform(JammyAspnetDockerfilePath, new string[] { "jammy" })),
                            CreateImage(
                                CreatePlatform(JammyChiseledAspnetDockerfilePath, new string[] { "jammy-chiseled" }))),
                        CreateRepo(
                            SdkRepo,
                            CreateImage(
                                CreatePlatform(JammySdkDockerfilePath, new string[] { "jammy" }))),
                        CreateRepo(
                            MonitorRepo,
                            CreateImage(
                                CreatePlatform(JammyChiseledMonitorDockerfilePath, new string[] { "jammy-chiseled" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = RuntimeDepsRepo,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                JammyRuntimeDepsDockerfilePath,
                                                baseImageDigest: $"{baseImage}@{baseImageDigest}-diff",
                                                simpleTags: new List<string> { "tag1" })
                                        }
                                    }
                                }
                            },
                            new RepoData
                            {
                                Repo = RuntimeRepo
                            },
                            new RepoData
                            {
                                Repo = AspnetRepo
                            },
                            new RepoData
                            {
                                Repo = SdkRepo
                            },
                            new RepoData
                            {
                                Repo = MonitorRepo
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new()
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(JammyRuntimeDepsDockerfilePath, new FromImageInfo(baseImage, baseImageDigest)),
                        new DockerfileInfo(JammyChiseledRuntimeDepsDockerfilePath, new FromImageInfo("scratch", null)),
                        new DockerfileInfo(JammyRuntimeDockerfilePath, new FromImageInfo($"{RuntimeDepsRepo}:jammy", null)),
                        new DockerfileInfo(JammyChiseledRuntimeDockerfilePath, new FromImageInfo($"{RuntimeDepsRepo}:jammy-chiseled", null)),
                        new DockerfileInfo(JammyAspnetDockerfilePath, new FromImageInfo($"{RuntimeRepo}:jammy", null)),
                        new DockerfileInfo(JammyChiseledAspnetDockerfilePath, new FromImageInfo($"{RuntimeRepo}:jammy-chiseled", null)),
                        new DockerfileInfo(JammySdkDockerfilePath, new FromImageInfo($"{AspnetRepo}:jammy", null)),
                        new DockerfileInfo(JammyChiseledMonitorDockerfilePath,
                            new FromImageInfo($"{SdkRepo}:jammy", null),
                            new FromImageInfo($"{AspnetRepo}:jammy-chiseled", null)),
                    }
                }
            };

            using (TestContext context =
                new(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new()
                {
                    {
                        subscriptionInfos[0].Subscription,
                        new List<string>
                        {
                            JammyRuntimeDepsDockerfilePath,
                            JammyRuntimeDockerfilePath,
                            JammyAspnetDockerfilePath,
                            JammySdkDockerfilePath,
                            JammyChiseledMonitorDockerfilePath,
                            JammyChiseledAspnetDockerfilePath,
                            JammyChiseledRuntimeDockerfilePath,
                            JammyChiseledRuntimeDepsDockerfilePath,
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued build when
        /// a base image changes where the image referencing that base image has other
        /// images dependent upon it and no image info data exists.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_DependencyGraph_MissingImageInfo()
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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription("repo1"),
                    CreateManifest(
                        CreateRepo(
                            runtimeDepsRepo,
                            CreateImage(
                                CreatePlatform(runtimeDepsDockerfilePath, new string[] { "tag1" }))),
                        CreateRepo(
                            runtimeRepo,
                            CreateImage(
                                CreatePlatform(runtimeDockerfilePath, new string[] { "tag1" }))),
                        CreateRepo(
                            sdkRepo,
                            CreateImage(
                                CreatePlatform(sdkDockerfilePath, new string[] { "tag1" }))),
                        CreateRepo(
                            aspnetRepo,
                            CreateImage(
                                CreatePlatform(aspnetDockerfilePath, new string[] { "tag1" }))),
                        CreateRepo(
                            otherRepo,
                            CreateImage(
                                CreatePlatform(otherDockerfilePath, new string[] { "tag1" })))),
                    new ImageArtifactDetails
                    {
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(runtimeDepsDockerfilePath, new FromImageInfo(baseImage, baseImageDigest)),
                        new DockerfileInfo(runtimeDockerfilePath, new FromImageInfo($"{runtimeDepsRepo}:tag1", null)),
                        new DockerfileInfo(sdkDockerfilePath, new FromImageInfo($"{aspnetRepo}:tag1", null)),
                        new DockerfileInfo(aspnetDockerfilePath, new FromImageInfo($"{runtimeRepo}:tag1", null)),
                        new DockerfileInfo(otherDockerfilePath, new FromImageInfo(otherImage, otherImageDigest))
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
                        new List<string>
                        {
                            runtimeDepsDockerfilePath,
                            runtimeDockerfilePath,
                            aspnetDockerfilePath,
                            sdkDockerfilePath,
                            otherDockerfilePath
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

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: "base1@base1digest-diff",
                                                simpleTags: new List<string> { "tag1" }),
                                            CreatePlatform(
                                                dockerfile2Path,
                                                baseImageDigest: "base2@base2digest",
                                                simpleTags: new List<string> { "tag2" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base1", "base1digest")),
                        new DockerfileInfo(dockerfile2Path, new FromImageInfo("base2", "base2digest"))
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // Only one of the images has a changed digest
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
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
        /// Verifies an image will be marked to be rebuilt if its base image is not included in the list of image data.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_NoExistingImageData()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: "base1digest")
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base2", "base2digest")),
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
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
        /// Verifies that a Dockerfile with only an internal FROM should not be considered stale.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_InternalFromOnly()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";
            const string dockerfile2Path = "dockerfile2/Dockerfile";

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" })),
                            CreateImage(
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(dockerfile1Path),
                                            CreatePlatform(
                                                dockerfile2Path,
                                                baseImageDigest: "base1@base1digest",
                                                simpleTags: new List<string> { "tag1" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo($"{repo1}:tag2", null)),
                        new DockerfileInfo(dockerfile2Path, new FromImageInfo("base1", "base1digest"))
                    }
                }
            };

            using (TestContext context =
                new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // No paths are expected
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>();

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued build for a
        /// scenario involving two platforms sharing the same Dockerfile.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_SharedDockerfile()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" }, osVersion: "alpine3.10"),
                                CreatePlatform(dockerfile1Path, new string[] { "tag2" }, osVersion: "alpine3.11")))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: "base1digest",
                                                osVersion: "Alpine 3.10"),
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: "base2digest-diff",
                                                osVersion: "Alpine 3.11")
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
                new Dictionary<GitFile, List<DockerfileInfo>>
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base1", "base1digest")),
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base2", "base2digest"))
                    }
                }
            };

            using (TestContext context = new TestContext(subscriptionInfos, dockerfileInfos))
            {
                await context.ExecuteCommandAsync();

                // Only one of the images has a changed digest
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptionInfos[0].Subscription,
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
        /// Verifies that the check for a stale base image is done by targeting the tag override rather than the tag
        /// defined in the Dockerfile.
        /// </summary>
        [Fact]
        public async Task GetStaleImagesCommand_BaseImageTagOverride()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1/Dockerfile";
            const string dockerfile2Path = "dockerfile2/Dockerfile";
            const string CustomRegistry = "my-registry";

            SubscriptionInfo[] subscriptionInfos = new SubscriptionInfo[]
            {
                new SubscriptionInfo(
                    CreateSubscription(repo1),
                    CreateManifest(
                        CreateRepo(
                            repo1,
                            CreateImage(
                                CreatePlatform(dockerfile1Path, new string[] { "tag1" }),
                                CreatePlatform(dockerfile2Path, new string[] { "tag2" })))),
                    new ImageArtifactDetails
                    {
                        Repos =
                        {
                            new RepoData
                            {
                                Repo = repo1,
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                dockerfile1Path,
                                                baseImageDigest: $"{CustomRegistry}/base1@base1digest",
                                                simpleTags: new List<string> { "tag1" }),
                                            CreatePlatform(
                                                dockerfile2Path,
                                                baseImageDigest: $"{CustomRegistry}/base2@alternate-base2digest",
                                                simpleTags: new List<string> { "tag2" })
                                        }
                                    }
                                }
                            }
                        }
                    }
                )
            };

            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos = new()
            {
                {
                    subscriptionInfos[0].Subscription.Manifest,
                    new List<DockerfileInfo>
                    {
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo("base1", "base1digest")),
                        new DockerfileInfo(dockerfile2Path, new FromImageInfo("base2", "base2digest"))
                    }
                }
            };

            using (TestContext context = new(subscriptionInfos, dockerfileInfos))
            {
                context.ImageDigests.Add($"{CustomRegistry}/base1", "alternate-base1digest");
                context.ImageDigests.Add($"{CustomRegistry}/base2", "alternate-base2digest");

                // Override the image tags to target a custom registry
                context.Command.Options.BaseImageOverrideOptions.RegexPattern = "(base.*)";
                context.Command.Options.BaseImageOverrideOptions.Substitution = "my-registry/$1";

                await context.ExecuteCommandAsync();

                // Only one of the images has a changed digest
                // It should be comparing against the digest of the image from the override.
                Dictionary<Subscription, IList<string>> expectedPathsBySubscription = new()
                {
                    {
                        subscriptionInfos[0].Subscription,
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

        private static Subscription CreateSubscription(
            string repoName,
            int index = 0,
            string osType = null,
            [CallerMemberName] string testMethodName = null)
        {
            return new Subscription
            {
                PipelineTrigger = new PipelineTrigger
                {
                    Id = 1,
                    PathVariable = "--my-path"
                },
                Manifest = new SubscriptionManifest
                {
                    Branch = "testBranch" + index,
                    Repo = repoName,
                    Owner = GetRepoOwner(testMethodName, index.ToString()),
                    Path = "testmanifest.json"
                },
                ImageInfo = new GitFile
                {

                    Owner = GetStaleImagesCommandTests.GitHubOwner,
                    Repo = GetStaleImagesCommandTests.GitHubRepo,
                    Branch = GetStaleImagesCommandTests.GitHubBranch,
                    Path = "docker/image-info.json"
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
            private readonly IHttpClientProvider httpClientFactory;
            private readonly GetStaleImagesCommand command;
            private readonly Mock<ILoggerService> loggerServiceMock = new Mock<ILoggerService>();
            private readonly string osType;
            private readonly IOctokitClientFactory octokitClientFactory;
            private readonly IGitService gitService;

            private const string VariableName = "my-var";

            public Mock<IManifestService> ManifestServiceMock { get; }

            public Mock<IManifestServiceFactory> ManifestServiceFactoryMock { get; }

            public GetStaleImagesCommand Command { get => command; }

            public IDictionary<string, string> ImageDigests { get => imageDigests; }

            /// <summary>
            /// Initializes a new instance of <see cref="TestContext"/>.
            /// </summary>
            /// <param name="subscriptionInfos">Mapping of data to subscriptions.</param>
            /// <param name="dockerfileInfos">A mapping of Git repos to their associated set of Dockerfiles.</param>
            /// <param name="osType">The OS type to filter the command with.</param>
            public TestContext(
                SubscriptionInfo[] subscriptionInfos,
                Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos,
                string osType = "*")
            {
                this.osType = osType;
                this.subscriptionsPath = this.SerializeJsonObjectToTempFile(
                    subscriptionInfos.Select(tuple => tuple.Subscription).ToArray());

                // Cache image digests lookup
                foreach (FromImageInfo fromImage in
                    dockerfileInfos.Values.SelectMany(infos => infos).SelectMany(info => info.FromImages))
                {
                    if (fromImage.Name != null)
                    {
                        this.imageDigests[fromImage.Name] = fromImage.Digest;
                    }
                }

                TeamProject project = new TeamProject
                {
                    Id = Guid.NewGuid()
                };

                this.gitService = CreateGitService(subscriptionInfos, dockerfileInfos);
                this.octokitClientFactory = CreateOctokitClientFactory(subscriptionInfos);

                (ManifestServiceFactoryMock, ManifestServiceMock) = CreateManifestServiceMocks();

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
                GetStaleImagesCommand command = new(
                    this.ManifestServiceFactoryMock.Object, this.loggerServiceMock.Object, this.octokitClientFactory, this.gitService);
                command.Options.SubscriptionOptions.SubscriptionsPath = this.subscriptionsPath;
                command.Options.VariableName = VariableName;
                command.Options.FilterOptions.OsType = this.osType;
                command.Options.GitOptions.Email = "test";
                command.Options.GitOptions.Username = "test";
                command.Options.GitOptions.AuthToken = "test";
                return command;
            }

            private static IOctokitClientFactory CreateOctokitClientFactory(SubscriptionInfo[] subscriptionInfos)
            {
                Mock<Octokit.ITreesClient> treesClientMock = new();
                Mock<Octokit.IBlobsClient> blobsClientMock = new();

                foreach (SubscriptionInfo subscriptionInfo in subscriptionInfos)
                {
                    if (subscriptionInfo.ImageInfo != null)
                    {

                        string generatedFakeSha = Guid.NewGuid().ToString();
                        treesClientMock
                            .Setup(o => o.Get(
                                subscriptionInfo.Subscription.ImageInfo.Owner,
                                subscriptionInfo.Subscription.ImageInfo.Repo,
                                It.Is<string>(reference => reference.StartsWith(subscriptionInfo.Subscription.ImageInfo.Branch))))
                            .ReturnsAsync(new Octokit.TreeResponse("sha", "url", new List<Octokit.TreeItem>
                            {
                                new Octokit.TreeItem(
                                    "dummy-path",
                                    "mode",
                                    Octokit.TreeType.Blob,
                                    0,
                                    "sha",
                                    "url"),
                                new Octokit.TreeItem(
                                    Path.GetFileName(subscriptionInfo.Subscription.ImageInfo.Path),
                                    "mode",
                                    Octokit.TreeType.Blob,
                                    0,
                                    generatedFakeSha,
                                    "url")
                            }.AsReadOnly(), false));

                        string imageInfoContents = JsonConvert.SerializeObject(subscriptionInfo.ImageInfo);
                        byte[] imageInfoBytes = Encoding.UTF8.GetBytes(imageInfoContents);
                        string imageInfoBase64 = Convert.ToBase64String(imageInfoBytes);

                        blobsClientMock
                            .Setup(o => o.Get(
                                subscriptionInfo.Subscription.ImageInfo.Owner,
                                subscriptionInfo.Subscription.ImageInfo.Repo,
                                generatedFakeSha))
                            .ReturnsAsync(new Octokit.Blob("nodeId", imageInfoBase64, Octokit.EncodingType.Base64, generatedFakeSha, 0));
                    }
                }

                Mock<IOctokitClientFactory> octokitClientFactoryMock = new();
                octokitClientFactoryMock
                    .Setup(o => o.CreateTreesClient(It.IsAny<Octokit.IApiConnection>()))
                    .Returns(treesClientMock.Object);
                octokitClientFactoryMock
                    .Setup(o => o.CreateBlobsClient(It.IsAny<Octokit.IApiConnection>()))
                    .Returns(blobsClientMock.Object);

                return octokitClientFactoryMock.Object;
            }

            private static bool IsMatchingBranch(GitHubBranch branch)
            {
                return branch.Name == GitHubBranch &&
                    branch.Project.Name == GitHubRepo &&
                    branch.Project.Owner == GitHubOwner;
            }

            /// <summary>
            /// Returns a <see cref="IGitService"/> that implements the clone operation.
            /// </summary>
            /// <param name="subscriptionInfos">Mapping of data to subscriptions.</param>
            /// <param name="dockerfileInfos">A mapping of Git repos to their associated set of Dockerfiles.</param>
            private IGitService CreateGitService(
                SubscriptionInfo[] subscriptionInfos,
                Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos)
            {
                Mock<IGitService> gitServiceMock = new();

                foreach (SubscriptionInfo subscriptionInfo in subscriptionInfos)
                {
                    Subscription subscription = subscriptionInfo.Subscription;
                    List<DockerfileInfo> repoDockerfileInfos = dockerfileInfos[subscription.Manifest];

                    string url = $"https://github.com/{subscription.Manifest.Owner}/{subscription.Manifest.Repo}.git";
                    gitServiceMock
                        .Setup(o => o.CloneRepository(url, It.IsAny<string>(), It.Is<CloneOptions>(options => options.BranchName == subscription.Manifest.Branch)))
                        .Callback((string url, string repoPath, CloneOptions options) =>
                        {
                            GenerateRepo(repoPath, subscription, subscriptionInfo.Manifest, repoDockerfileInfos);
                        })
                        .Returns(Mock.Of<IRepository>());

                }

                return gitServiceMock.Object;
            }

            /// <summary>
            /// Generates a directory that represents the contents of a GitHub repo.
            /// </summary>
            /// <param name="repoPath">Directory path to store the repo contents.</param>
            /// <param name="subscription">The subscription associated with the GitHub repo.</param>
            /// <param name="manifest">Manifest model associated with the subscription.</param>
            /// <param name="repoDockerfileInfos">Set of <see cref="DockerfileInfo"/> objects that describe the Dockerfiles contained in the repo.</param>
            private void GenerateRepo(
                string repoPath,
                Subscription subscription,
                Manifest manifest,
                List<DockerfileInfo> repoDockerfileInfos)
            {
                // Create folder that represents the repo contents.
                Directory.CreateDirectory(repoPath);

                // Serialize the manifest model to a file in the repo folder.
                string manifestPath = Path.Combine(repoPath, subscription.Manifest.Path);
                File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

                foreach (DockerfileInfo dockerfileInfo in repoDockerfileInfos)
                {
                    GenerateDockerfile(dockerfileInfo, repoPath);
                }
            }

            /// <summary>
            /// Generates a Dockerfile from the <see cref="DockerfileInfo"/> metatada and stores it the specified path.
            /// </summary>
            /// <param name="dockerfileInfo">The metadata for the Dockerfile.</param>
            /// <param name="destinationPath">Folder path to store the Dockerfile.</param>
            private static void GenerateDockerfile(DockerfileInfo dockerfileInfo, string destinationPath)
            {
                string dockerfileContents = string.Empty;

                foreach (FromImageInfo fromImage in dockerfileInfo.FromImages)
                {
                    dockerfileContents += $"FROM {fromImage.Name}{Environment.NewLine}";
                }

                string dockerfilePath = Directory.CreateDirectory(
                    Path.Combine(destinationPath, Path.GetDirectoryName(dockerfileInfo.DockerfilePath))).FullName;

                File.WriteAllText(
                    Path.Combine(dockerfilePath, Path.GetFileName(dockerfileInfo.DockerfilePath)), dockerfileContents);
            }

            private (Mock<IManifestServiceFactory>, Mock<IManifestService>) CreateManifestServiceMocks()
            {
                Mock<IManifestService> manifestServiceMock = new()
                {
                    CallBase = true
                };

                manifestServiceMock
                    .Setup(o => o.GetManifestAsync(It.IsAny<string>(), false))
                    .ReturnsAsync((string image, bool isDryRun) =>
                        new ManifestQueryResult(this.imageDigests[image], new JsonObject()));

                Mock<IManifestServiceFactory> manifestServiceFactoryMock =
                    ManifestServiceHelper.CreateManifestServiceFactoryMock(manifestServiceMock.Object);

                return (manifestServiceFactoryMock, manifestServiceMock);
            }

            /// <summary>
            /// Returns a value indicating whether the <see cref="Build"/> object contains the expected state.
            /// </summary>
            /// <param name="build">The <see cref="Build"/> to validate.</param>
            /// <param name="subscription">Subscription object that contains metadata to compare against the <paramref name="build"/>.</param>
            /// <param name="expectedPaths">The set of expected path arguments that should have been passed to the build.</param>
            private static bool FilterBuildToSubscription(WebApi.Build build, Subscription subscription, IList<string> expectedPaths)
            {
                return build.Definition.Id == subscription.PipelineTrigger.Id &&
                    build.SourceBranch == subscription.Manifest.Branch &&
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
            }
        }

        private class DockerfileInfo
        {
            public DockerfileInfo(string dockerfilePath, params FromImageInfo[] fromImages)
            {
                this.DockerfilePath = dockerfilePath;
                this.FromImages = fromImages;
            }

            public string DockerfilePath { get; }
            public FromImageInfo[] FromImages { get; }
        }

        private class FromImageInfo
        {
            public FromImageInfo (string name, string digest)
            {
                Name = name;
                Digest = digest;
            }

            public string Digest { get; }
            public string Name { get; }
        }

        private class PagedList<T> : List<T>, IPagedList<T>
        {
            public string ContinuationToken
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }
        }

        private class SubscriptionInfo
        {
            public SubscriptionInfo(Models.Subscription.Subscription subscription, Manifest manifest, ImageArtifactDetails imageInfo)
            {
                Subscription = subscription;
                Manifest = manifest;
                ImageInfo = imageInfo;
            }

            public Models.Subscription.Subscription Subscription { get; }
            public Manifest Manifest { get; }
            public ImageArtifactDetails ImageInfo { get; }
        }
    }
}
