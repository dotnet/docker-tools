#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;
using WebApi = Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class GetStaleImagesCommandTests
{
    #nullable enable annotations
    public TestContext? TestContext { get; set; }

    #nullable disable annotations

    private const string GitHubBranch = "my-branch";
    private const string GitHubRepo = "my-repo";
    private const string GitHubOwner = "my-owner";

    /// <summary>
    /// Verifies the correct path arguments are passed to the queued build for a basic
    /// scenario involving one image that has changed.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_SingleDigestChanged()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";
        const string dockerfile2Path = "dockerfile2/Dockerfile";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"]),
                            CreatePlatform(dockerfile2Path, ["tag2"])))),
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
                                            simpleTags: ["tag1"]),
                                        CreatePlatform(
                                            dockerfile2Path,
                                            baseImageDigest: "base2@base2digest",
                                            simpleTags: ["tag2"])
                                    }
                                }
                            }
                        }
                    }
                }
            )
        ];

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

        using TestFixture fixture = new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies the correct path arguments are passed to the queued build for a multi-stage
    /// Dockerfile scenario involving one image that has changed.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_MultiStage_SingleDigestChanged()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";
        const string dockerfile2Path = "dockerfile2/Dockerfile";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"]),
                            CreatePlatform(dockerfile2Path, ["tag2"])))),
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
                                            simpleTags: ["tag1"]),
                                        CreatePlatform(
                                            dockerfile2Path,
                                            baseImageDigest: "base3@base3digest",
                                            simpleTags: ["tag2"])
                                    }
                                }
                            }
                        }
                    }
                }
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies that a subscription will be skipped if it's associated with a different OS type than the command is assigned with.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_OsTypeFiltering_MatchingCommandFilter()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1";
        const string dockerfile2Path = "dockerfile2";
        const string dockerfile3Path = "dockerfile3";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1, osType: "windows"),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"], OS.Windows),
                            CreatePlatform(dockerfile2Path, ["tag2"], OS.Linux),
                            CreatePlatform(dockerfile3Path, ["tag3"], OS.Windows)))),
                new ImageArtifactDetails()
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos, commandOsType);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies that a subscription will be skipped if it's associated with a different OS type than the command is assigned with.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_OsTypeFiltering_NonMatchingCommandFilter()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1";
        const string dockerfile2Path = "dockerfile2";
        const string dockerfile3Path = "dockerfile3";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1, osType: "windows"),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"], OS.Windows),
                            CreatePlatform(dockerfile2Path, ["tag2"], OS.Linux),
                            CreatePlatform(dockerfile3Path, ["tag3"], OS.Windows)))),
                new ImageArtifactDetails()
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos, commandOsType);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

        Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
            [];

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies the correct path arguments are passed to the queued build when
    /// the images have no data reflected in the image info data.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_MissingImageInfo()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";
        const string dockerfile2Path = "dockerfile2/Dockerfile";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"]),
                            CreatePlatform(dockerfile2Path, ["tag2"])))),
                new ImageArtifactDetails()
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies the correct path arguments are passed to the queued builds for two
    /// subscriptions that have changed images.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_MultiSubscription()
    {
        const string repo1 = "test-repo";
        const string repo2 = "test-repo2";
        const string repo3 = "test-repo3";
        const string dockerfile1Path = "dockerfile1/Dockerfile";
        const string dockerfile2Path = "dockerfile2/Dockerfile";
        const string dockerfile3Path = "dockerfile3/Dockerfile";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1, 1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"])))),
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
                                            simpleTags: ["tag1"])
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
                            CreatePlatform(dockerfile2Path, ["tag2"]))),
                    CreateRepo(
                        repo3,
                        CreateImage(
                            CreatePlatform(dockerfile3Path, ["tag3"])))),
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
                                            simpleTags: ["tag1"])
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
                                            simpleTags: ["tag2"])
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
                                            simpleTags: ["tag3"])
                                    }
                                }
                            }
                        }
                    }
                }
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies that a base image's digest will be cached and not pulled for a subsequent image.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_BaseImageCaching()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";
        const string dockerfile2Path = "dockerfile2/Dockerfile";
        const string baseImage = "base1";
        const string baseImageDigest = "base1digest";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"]),
                            CreatePlatform(dockerfile2Path, ["tag2"])))),
                new ImageArtifactDetails
                {
                    Repos =
                    {
                        new RepoData
                        {
                            Repo = repo1,
                            Images =
                            [
                                new ImageData
                                {
                                    Platforms =
                                    [
                                        CreatePlatform(
                                            dockerfile1Path,
                                            baseImageDigest: baseImageDigest + "-diff",
                                            simpleTags: ["tag1"]),
                                        CreatePlatform(
                                            dockerfile2Path,
                                            baseImageDigest: baseImageDigest + "-diff",
                                            simpleTags: ["tag2"])
                                    ]
                                }
                            ]
                        }
                    }
                }
            )
        ];


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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);

        fixture.ManifestServiceMock
            .Verify(o => o.GetManifestAsync(baseImage, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that no build will be queued if the base image has not changed.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_NoBaseImageChange()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";
        const string baseImage = "base1";
        const string baseImageDigest = "base1digest";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"])))),
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
                                            simpleTags: ["tag1"])
                                    }
                                }
                            }
                        }
                    }
                }
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

        // No paths are expected
        Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
            [];

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies that no build will be queued for a Dockerfile with no base image.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_NoBaseImage()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"])))),
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
                                            simpleTags: ["tag1"])
                                    }
                                }
                            }
                        }
                    }
                }
            )
        ];

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

        using TestFixture fixture = new(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

        // No paths are expected
        Dictionary<Subscription, IList<string>> expectedPathsBySubscription = [];

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies the correct path arguments are passed to the queued build when
    /// a base image changes where the image referencing that base image has other
    /// images dependent upon it.
    /// </summary>
    [TestMethod]
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

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription("repo1"),
                CreateManifest(
                    CreateRepo(
                        runtimeDepsRepo,
                        CreateImage(
                            CreatePlatform(runtimeDepsDockerfilePath, ["tag1"]))),
                    CreateRepo(
                        runtimeRepo,
                        CreateImage(
                            CreatePlatform(runtimeDockerfilePath, ["tag1"]))),
                    CreateRepo(
                        sdkRepo,
                        CreateImage(
                            CreatePlatform(sdkDockerfilePath, ["tag1"]))),
                    CreateRepo(
                        aspnetRepo,
                        CreateImage(
                            CreatePlatform(aspnetDockerfilePath, ["tag1"]))),
                    CreateRepo(
                        otherRepo,
                        CreateImage(
                            CreatePlatform(otherDockerfilePath, ["tag1"])))),
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
                                            simpleTags: ["tag1"])
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
                                            simpleTags: ["tag1"])
                                    }
                                }
                            }
                        }
                    }
                }
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
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
    [TestMethod]
    public async Task GetStaleImagesCommand_DependencyGraph_TwoRoots()
    {
        const string runtimeDepsRepo = "runtime-deps";
        const string runtimeRepo = "runtime";
        const string aspnetRepo = "aspnet";
        const string sdkRepo = "sdk";
        const string monitorRepo = "monitor";

        const string jammyRuntimeDepsDockerfilePath = "runtime-deps/jammy/Dockerfile";
        const string jammyRuntimeDockerfilePath = "runtime/jammy/Dockerfile";
        const string jammyAspnetDockerfilePath = "aspnet/jammy/Dockerfile";
        const string jammySdkDockerfilePath = "sdk/jammy/Dockerfile";
        const string jammyChiseledRuntimeDepsDockerfilePath = "runtime-deps/jammy-chiseled/Dockerfile";
        const string jammyChiseledRuntimeDockerfilePath = "runtime/jammy-chiseled/Dockerfile";
        const string jammyChiseledAspnetDockerfilePath = "aspnet/jammy-chiseled/Dockerfile";
        const string jammyChiseledMonitorDockerfilePath = "monitor/jammy-chiseled/Dockerfile";

        const string baseImage = "base1";
        const string baseImageDigest = "base1digest";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription("repo1"),
                CreateManifest(
                    CreateRepo(
                        runtimeDepsRepo,
                        CreateImage(
                            CreatePlatform(jammyRuntimeDepsDockerfilePath, ["jammy"])),
                        CreateImage(
                            CreatePlatform(jammyChiseledRuntimeDepsDockerfilePath, ["jammy-chiseled"]))),
                    CreateRepo(
                        runtimeRepo,
                        CreateImage(
                            CreatePlatform(jammyRuntimeDockerfilePath, ["jammy"])),
                        CreateImage(
                            CreatePlatform(jammyChiseledRuntimeDockerfilePath, ["jammy-chiseled"]))),
                    CreateRepo(
                        aspnetRepo,
                        CreateImage(
                            CreatePlatform(jammyAspnetDockerfilePath, ["jammy"])),
                        CreateImage(
                            CreatePlatform(jammyChiseledAspnetDockerfilePath, ["jammy-chiseled"]))),
                    CreateRepo(
                        sdkRepo,
                        CreateImage(
                            CreatePlatform(jammySdkDockerfilePath, ["jammy"]))),
                    CreateRepo(
                        monitorRepo,
                        CreateImage(
                            CreatePlatform(jammyChiseledMonitorDockerfilePath, ["jammy-chiseled"])))),
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
                                            jammyRuntimeDepsDockerfilePath,
                                            baseImageDigest: $"{baseImage}@{baseImageDigest}-diff",
                                            simpleTags: ["tag1"])
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
                            Repo = aspnetRepo
                        },
                        new RepoData
                        {
                            Repo = sdkRepo
                        },
                        new RepoData
                        {
                            Repo = monitorRepo
                        }
                    }
                }
            )
        ];

        Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos =
            new()
        {
            {
                subscriptionInfos[0].Subscription.Manifest,
                new List<DockerfileInfo>
                {
                    new DockerfileInfo(jammyRuntimeDepsDockerfilePath, new FromImageInfo(baseImage, baseImageDigest)),
                    new DockerfileInfo(jammyChiseledRuntimeDepsDockerfilePath, new FromImageInfo("scratch", null)),
                    new DockerfileInfo(jammyRuntimeDockerfilePath, new FromImageInfo($"{runtimeDepsRepo}:jammy", null)),
                    new DockerfileInfo(jammyChiseledRuntimeDockerfilePath, new FromImageInfo($"{runtimeDepsRepo}:jammy-chiseled", null)),
                    new DockerfileInfo(jammyAspnetDockerfilePath, new FromImageInfo($"{runtimeRepo}:jammy", null)),
                    new DockerfileInfo(jammyChiseledAspnetDockerfilePath, new FromImageInfo($"{runtimeRepo}:jammy-chiseled", null)),
                    new DockerfileInfo(jammySdkDockerfilePath, new FromImageInfo($"{aspnetRepo}:jammy", null)),
                    new DockerfileInfo(jammyChiseledMonitorDockerfilePath,
                        new FromImageInfo($"{sdkRepo}:jammy", null),
                        new FromImageInfo($"{aspnetRepo}:jammy-chiseled", null)),
                }
            }
        };

        using TestFixture fixture =
            new(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

        Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
            new()
        {
                {
                    subscriptionInfos[0].Subscription,
                    new List<string>
                    {
                        jammyRuntimeDepsDockerfilePath,
                        jammyRuntimeDockerfilePath,
                        jammyAspnetDockerfilePath,
                        jammySdkDockerfilePath,
                        jammyChiseledMonitorDockerfilePath,
                        jammyChiseledAspnetDockerfilePath,
                        jammyChiseledRuntimeDockerfilePath,
                        jammyChiseledRuntimeDepsDockerfilePath,
                    }
                }
        };

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies the correct path arguments are passed to the queued build when
    /// a base image changes where the image referencing that base image has other
    /// images dependent upon it and no image info data exists.
    /// </summary>
    [TestMethod]
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

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription("repo1"),
                CreateManifest(
                    CreateRepo(
                        runtimeDepsRepo,
                        CreateImage(
                            CreatePlatform(runtimeDepsDockerfilePath, ["tag1"]))),
                    CreateRepo(
                        runtimeRepo,
                        CreateImage(
                            CreatePlatform(runtimeDockerfilePath, ["tag1"]))),
                    CreateRepo(
                        sdkRepo,
                        CreateImage(
                            CreatePlatform(sdkDockerfilePath, ["tag1"]))),
                    CreateRepo(
                        aspnetRepo,
                        CreateImage(
                            CreatePlatform(aspnetDockerfilePath, ["tag1"]))),
                    CreateRepo(
                        otherRepo,
                        CreateImage(
                            CreatePlatform(otherDockerfilePath, ["tag1"])))),
                new ImageArtifactDetails
                {
                }
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies the correct path arguments are passed to the queued build when an image
    /// built from a custom named Dockerfile.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_CustomDockerfilePath()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "path/to/Dockerfile.custom";
        const string dockerfile2Path = "path/to/Dockerfile";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"]),
                            CreatePlatform(dockerfile2Path, ["tag2"])))),
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
                                            simpleTags: ["tag1"]),
                                        CreatePlatform(
                                            dockerfile2Path,
                                            baseImageDigest: "base2@base2digest",
                                            simpleTags: ["tag2"])
                                    }
                                }
                            }
                        }
                    }
                }
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies an image will be marked to be rebuilt if its base image is not included in the list of image data.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_NoExistingImageData()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"])))),
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
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies that a Dockerfile with only an internal FROM should not be considered stale.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_InternalFromOnly()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";
        const string dockerfile2Path = "dockerfile2/Dockerfile";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"])),
                        CreateImage(
                            CreatePlatform(dockerfile2Path, ["tag2"])))),
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
                                            simpleTags: ["tag1"])
                                    }
                                }
                            }
                        }
                    }
                }
            )
        ];

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

        using TestFixture fixture =
            new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

        // No paths are expected
        Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
            [];

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies the correct path arguments are passed to the queued build for a
    /// scenario involving two platforms sharing the same Dockerfile.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_SharedDockerfile()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"], osVersion: "alpine3.10"),
                            CreatePlatform(dockerfile1Path, ["tag2"], osVersion: "alpine3.11")))),
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
        ];

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

        using TestFixture fixture = new TestFixture(subscriptionInfos, dockerfileInfos);
        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Verifies that the check for a stale base image is done by targeting the tag override rather than the tag
    /// defined in the Dockerfile.
    /// </summary>
    [TestMethod]
    public async Task GetStaleImagesCommand_BaseImageTagOverride()
    {
        const string repo1 = "test-repo";
        const string dockerfile1Path = "dockerfile1/Dockerfile";
        const string dockerfile2Path = "dockerfile2/Dockerfile";
        const string customRegistry = "my-registry.io";

        SubscriptionInfo[] subscriptionInfos =
        [
            new SubscriptionInfo(
                CreateSubscription(repo1),
                CreateManifest(
                    CreateRepo(
                        repo1,
                        CreateImage(
                            CreatePlatform(dockerfile1Path, ["tag1"]),
                            CreatePlatform(dockerfile2Path, ["tag2"])))),
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
                                            baseImageDigest: $"{customRegistry}/base1@base1digest",
                                            simpleTags: ["tag1"]),
                                        CreatePlatform(
                                            dockerfile2Path,
                                            baseImageDigest: $"{customRegistry}/base2@alternate-base2digest",
                                            simpleTags: ["tag2"])
                                    }
                                }
                            }
                        }
                    }
                }
            )
        ];

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

        using TestFixture fixture = new(subscriptionInfos, dockerfileInfos);
        fixture.ImageDigests.Add($"{customRegistry}/base1", "alternate-base1digest");
        fixture.ImageDigests.Add($"{customRegistry}/base2", "alternate-base2digest");

        // Override the image tags to target a custom registry
        fixture.Command.Options.BaseImageOverrideOptions.RegexPattern = "(base.*)";
        fixture.Command.Options.BaseImageOverrideOptions.Substitution = "my-registry.io/$1";

        await fixture.ExecuteCommandAsync(TestContext?.CancellationToken ?? default);

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

        fixture.Verify(expectedPathsBySubscription);
    }

    /// <summary>
    /// Use this method to generate a unique repo owner name for the tests. This ensures that each test
    /// uses a different name and prevents collisions when running the tests in parallel. This is because
    /// the <see cref="GetStaleImagesCommand"/> generates temp folders partially based on the name of
    /// the repo owner.
    /// </summary>
    private static string GetRepoOwner([CallerMemberName] string testMethodName = null, string suffix = null) => testMethodName + suffix;

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
    private class TestFixture : IDisposable
    {
        private readonly List<string> _filesToCleanup = [];
        private readonly List<string> _foldersToCleanup = [];
        private readonly Dictionary<string, string> _imageDigests = [];
        private readonly string _subscriptionsPath;
        private readonly Mock<ILogger<GetStaleImagesCommand>> _loggerServiceMock = new Mock<ILogger<GetStaleImagesCommand>>();
        private readonly string _osType;
        private readonly IOctokitClientFactory _octokitClientFactory;
        private readonly IGitService _gitService;

        private const string VariableName = "my-var";

        public Mock<IManifestService> ManifestServiceMock { get; }

        public Mock<IManifestServiceFactory> ManifestServiceFactoryMock { get; }

        public GetStaleImagesCommand Command { get; }

        public IDictionary<string, string> ImageDigests => _imageDigests;

        /// <summary>
        /// Initializes a new instance of <see cref="TestFixture"/>.
        /// </summary>
        /// <param name="subscriptionInfos">Mapping of data to subscriptions.</param>
        /// <param name="dockerfileInfos">A mapping of Git repos to their associated set of Dockerfiles.</param>
        /// <param name="osType">The OS type to filter the command with.</param>
        public TestFixture(
            SubscriptionInfo[] subscriptionInfos,
            Dictionary<GitFile, List<DockerfileInfo>> dockerfileInfos,
            string osType = "*")
        {
            _osType = osType;
            _subscriptionsPath = SerializeJsonObjectToTempFile(
                subscriptionInfos.Select(tuple => tuple.Subscription).ToArray());

            // Cache image digests lookup
            foreach (FromImageInfo fromImage in
                dockerfileInfos.Values.SelectMany(infos => infos).SelectMany(info => info.FromImages))
            {
                if (fromImage.Name != null)
                {
                    // Normalize the key using ImageName.Parse to match how the mock receives ImageName
                    string normalizedName = ImageName.Parse(fromImage.Name, autoResolveImpliedNames: true).ToString();
                    _imageDigests[normalizedName] = fromImage.Digest;
                }
            }

            TeamProject project = new TeamProject
            {
                Id = Guid.NewGuid()
            };

            _gitService = CreateGitService(subscriptionInfos, dockerfileInfos);
            _octokitClientFactory = CreateOctokitClientFactory(subscriptionInfos);

            (ManifestServiceFactoryMock, ManifestServiceMock) = CreateManifestServiceMocks();

            Command = CreateCommand();
        }

        public Task ExecuteCommandAsync(CancellationToken cancellationToken) => Command.ExecuteAsync(cancellationToken);

        /// <summary>
        /// Verifies the test execution to ensure the results match the expected state.
        /// </summary>
        public void Verify(IDictionary<Subscription, IList<string>> expectedPathsBySubscription)
        {
            IInvocation invocation = _loggerServiceMock.Invocations
                .First(invocation => invocation.Method.Name == nameof(ILogger.Log) &&
                    invocation.Arguments[2]?.ToString()?.StartsWith("##vso") == true);

            string message = invocation.Arguments[2].ToString();
            int variableNameStartIndex = message.IndexOf("=") + 1;
            string actualVariableName = message[variableNameStartIndex..message.IndexOf(";")];
            actualVariableName.ShouldBe(VariableName);

            string variableValue = message[(message.IndexOf("]") + 1)..];

            SubscriptionImagePaths[] pathsBySubscription =
                JsonConvert.DeserializeObject<SubscriptionImagePaths[]>(variableValue.Replace("\\\"", "\""));

            pathsBySubscription.Length.ShouldBe(expectedPathsBySubscription.Count);

            foreach (KeyValuePair<Subscription, IList<string>> kvp in expectedPathsBySubscription)
            {
                string[] actualPaths = pathsBySubscription
                    .First(imagePaths => imagePaths.SubscriptionId == kvp.Key.Id).ImagePaths;

                actualPaths.ShouldBe(kvp.Value);
            }
        }

        private string SerializeJsonObjectToTempFile(object jsonObject)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, JsonConvert.SerializeObject(jsonObject));
            _filesToCleanup.Add(path);
            return path;
        }

        private GetStaleImagesCommand CreateCommand()
        {
            GetStaleImagesCommand command = new(
                ManifestServiceFactoryMock.Object, TestHelper.CreateManifestJsonService(), _loggerServiceMock.Object, _octokitClientFactory, _gitService);
            command.Options.SubscriptionOptions.SubscriptionsPath = _subscriptionsPath;
            command.Options.VariableName = VariableName;
            command.Options.FilterOptions.Platform.OsType = _osType;
            command.Options.GitOptions.Email = "test";
            command.Options.GitOptions.Username = "test";
            command.Options.GitOptions.GitHubAuthOptions = new GitHubAuthOptions(AuthToken: "test");
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
                .Setup(o => o.CreateTreesClientAsync(It.IsAny<GitHubAuthOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(treesClientMock.Object);
            octokitClientFactoryMock
                .Setup(o => o.CreateBlobsClientAsync(It.IsAny<GitHubAuthOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(blobsClientMock.Object);

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
                    .Callback((string url, string repoPath, CloneOptions options) => GenerateRepo(repoPath, subscription, subscriptionInfo.Manifest, repoDockerfileInfos))
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
                .Setup(o => o.GetManifestAsync(It.IsAny<ImageName>(), false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ImageName image, bool isDryRun, CancellationToken _) =>
                    new ManifestQueryResult(_imageDigests[image], []));

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
            foreach (string file in _filesToCleanup)
            {
                File.Delete(file);
            }

            foreach (string folder in _foldersToCleanup)
            {
                Directory.Delete(folder, true);
            }
        }
    }

    private class DockerfileInfo(string dockerfilePath, params GetStaleImagesCommandTests.FromImageInfo[] fromImages)
    {
        public string DockerfilePath { get; } = dockerfilePath;
        public FromImageInfo[] FromImages { get; } = fromImages;
    }

    private class FromImageInfo(string name, string digest)
    {
        public string Digest { get; } = digest;
        public string Name { get; } = name;
    }

    private class PagedList<T> : List<T>, IPagedList<T>
    {
        public string ContinuationToken
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
    }

    private class SubscriptionInfo(Subscription subscription, Manifest manifest, ImageArtifactDetails imageInfo)
    {
        public Subscription Subscription { get; } = subscription;
        public Manifest Manifest { get; } = manifest;
        public ImageArtifactDetails ImageInfo { get; } = imageInfo;
    }
}
