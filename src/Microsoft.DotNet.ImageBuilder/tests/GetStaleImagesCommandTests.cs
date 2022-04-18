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
using System.Text;
using System.Threading.Tasks;
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

                context.ManifestToolServiceMock
                    .Verify(o => o.InspectAsync(baseImage, false), Times.Once);
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
                                CreatePlatformWithRepoBuildArg(runtimeDockerfilePath, runtimeDepsRepo, new string[] { "tag1" }))),
                        CreateRepo(
                            sdkRepo,
                            CreateImage(
                                CreatePlatformWithRepoBuildArg(sdkDockerfilePath, runtimeRepo, new string[] { "tag1" }))),
                        CreateRepo(
                            aspnetRepo,
                            CreateImage(
                                CreatePlatformWithRepoBuildArg(aspnetDockerfilePath, runtimeRepo, new string[] { "tag1" }))),
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
                        new DockerfileInfo(runtimeDockerfilePath, new FromImageInfo("tag1", null, isInternal: true)),
                        new DockerfileInfo(sdkDockerfilePath, new FromImageInfo("tag1", null, isInternal: true)),
                        new DockerfileInfo(aspnetDockerfilePath, new FromImageInfo("tag1", null, isInternal: true)),
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
                            sdkDockerfilePath,
                            aspnetDockerfilePath
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
                                CreatePlatformWithRepoBuildArg(runtimeDockerfilePath, runtimeDepsRepo, new string[] { "tag1" }))),
                        CreateRepo(
                            sdkRepo,
                            CreateImage(
                                CreatePlatformWithRepoBuildArg(sdkDockerfilePath, aspnetRepo, new string[] { "tag1" }))),
                        CreateRepo(
                            aspnetRepo,
                            CreateImage(
                                CreatePlatformWithRepoBuildArg(aspnetDockerfilePath, runtimeRepo, new string[] { "tag1" }))),
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
                        new DockerfileInfo(runtimeDockerfilePath, new FromImageInfo("tag1", null, isInternal: true)),
                        new DockerfileInfo(sdkDockerfilePath, new FromImageInfo("tag1", null, isInternal: true)),
                        new DockerfileInfo(aspnetDockerfilePath, new FromImageInfo("tag1", null, isInternal: true)),
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
                                CreatePlatformWithRepoBuildArg(dockerfile1Path, $"{repo1}:tag2", new string[] { "tag1" })),
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
                        new DockerfileInfo(dockerfile1Path, new FromImageInfo(null, null, isInternal: true)),
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

            private const string VariableName = "my-var";

            public Mock<IManifestToolService> ManifestToolServiceMock { get; }

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

                this.httpClientFactory = CreateHttpClientFactory(subscriptionInfos, dockerfileInfos);
                this.octokitClientFactory = CreateOctokitClientFactory(subscriptionInfos);

                this.ManifestToolServiceMock = this.CreateManifestToolServiceMock();
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
                    this.ManifestToolServiceMock.Object, this.httpClientFactory, this.loggerServiceMock.Object, this.octokitClientFactory);
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
            /// Returns an <see cref="IHttpClientProvider"/> that creates an <see cref="HttpClient"/> which 
            /// bypasses the network and return back pre-built responses for GitHub repo zip files.
            /// </summary>
            /// <param name="subscriptionInfos">Mapping of data to subscriptions.</param>
            /// <param name="dockerfileInfos">A mapping of Git repos to their associated set of Dockerfiles.</param>
            private IHttpClientProvider CreateHttpClientFactory(
                SubscriptionInfo[] subscriptionInfos,
                Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos)
            {
                Dictionary<string, HttpResponseMessage> responses = new Dictionary<string, HttpResponseMessage>();
                foreach (SubscriptionInfo subscriptionInfo in subscriptionInfos)
                {
                    Subscription subscription = subscriptionInfo.Subscription;
                    List<DockerfileInfo> repoDockerfileInfos = dockerfileInfos[subscription.Manifest];
                    string repoZipPath = GenerateRepoZipFile(subscription, subscriptionInfo.Manifest, repoDockerfileInfos);

                    responses.Add(
                        $"https://github.com/{subscription.Manifest.Owner}/{subscription.Manifest.Repo}/archive/{subscription.Manifest.Branch}.zip",
                        new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new ByteArrayContent(File.ReadAllBytes(repoZipPath))
                        });
                }

                HttpClient client = new HttpClient(new TestHttpMessageHandler(responses));

                Mock<IHttpClientProvider> httpClientFactoryMock = new Mock<IHttpClientProvider>();
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
                    Path.Combine(tempDir, $"{subscription.Manifest.Repo}-{subscription.Manifest.Branch}")).FullName;

                // Serialize the manifest model to a file in the repo folder.
                string manifestPath = Path.Combine(repoPath, subscription.Manifest.Path);
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
                string dockerfileContents = string.Empty;

                foreach (FromImageInfo fromImage in dockerfileInfo.FromImages)
                {
                    string repo = string.Empty;
                    if (fromImage.IsInternal)
                    {
                        repo = "$REPO:";
                    }

                    dockerfileContents += $"FROM {repo}{fromImage.Name}{Environment.NewLine}";
                }

                string dockerfilePath = Directory.CreateDirectory(
                    Path.Combine(destinationPath, Path.GetDirectoryName(dockerfileInfo.DockerfilePath))).FullName;

                File.WriteAllText(
                    Path.Combine(dockerfilePath, Path.GetFileName(dockerfileInfo.DockerfilePath)), dockerfileContents);
            }

            private Mock<IManifestToolService> CreateManifestToolServiceMock()
            {
                Mock<IManifestToolService> manifestToolServiceMock = new Mock<IManifestToolService>();
                manifestToolServiceMock
                    .Setup(o => o.InspectAsync(It.IsAny<string>(), false))
                    .ReturnsAsync((string image, bool isDryRun) =>
                        ManifestToolServiceHelper.CreateTagManifest(
                            ManifestToolService.ManifestListMediaType, this.imageDigests[image]));
                return manifestToolServiceMock;
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

                this.command?.Dispose();
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
            public FromImageInfo (string name, string digest, bool isInternal = false)
            {
                Name = name;
                Digest = digest;
                IsInternal = isInternal;
            }

            public string Digest { get; }
            public bool IsInternal { get; }
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
