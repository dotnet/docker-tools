// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Valleysoft.DockerfileModel;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.VisualBasic.CompilerServices;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class WaitForMcrImageIngestionCommandTests
    {
        [Fact]
        public async Task SuccessfulPublish()
        {
            DateTime baselineTime = DateTime.Now;
            const string manifestDigest1 = "repo@sha256:manifestDigest1";
            const string sharedTag1 = "sharedTag1";
            const string sharedTag2 = "sharedTag2";
            const string platformTag1 = "platformTag1";
            const string platformTag2 = "platformTag2";
            const string platformTag3 = "platformTag3";
            const string repo1 = "repo1";
            const string repo2 = "repo2";
            const string platformDigest1 = "repo@sha256:platformDigest1";
            const string platformDigest2 = "repo@sha256:platformDigest2";

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();

            ImageStatus previousSharedTag1ImageStatus = new ImageStatus
            {
                Tag = sharedTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(-1),
                OverallStatus = StageStatus.Succeeded
            };

            ImageStatus sharedTag1ImageStatus = new ImageStatus
            {
                Tag = sharedTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.NotStarted
            };

            ImageStatus sharedTag2ImageStatus = new ImageStatus
            {
                Tag = sharedTag2,
                TargetRepository = repo1,
                QueueTime = baselineTime,
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag1ImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag2ImageStatus = new ImageStatus
            {
                Tag = platformTag2,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag3ImageStatus = new ImageStatus
            {
                Tag = platformTag3,
                TargetRepository = repo2,
                QueueTime = baselineTime.AddSeconds(1),
                OverallStatus = StageStatus.Succeeded
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new Dictionary<string, IEnumerator<ImageResult>>
            {
                {
                    ImageName.Parse(manifestDigest1).Digest,
                    new List<ImageResult>
                    {
                        new ImageResult
                        {
                            Digest = manifestDigest1,
                            Value = new List<ImageStatus>
                            {
                                previousSharedTag1ImageStatus,
                                sharedTag1ImageStatus,
                                sharedTag2ImageStatus,
                            }
                        },
                        new ImageResult
                        {
                            Digest = manifestDigest1,
                            Value = new List<ImageStatus>
                            {
                                previousSharedTag1ImageStatus,
                                Clone(sharedTag1ImageStatus, StageStatus.Processing),
                                sharedTag2ImageStatus,
                            }
                        },
                        new ImageResult
                        {
                            Digest = manifestDigest1,
                            Value = new List<ImageStatus>
                            {
                                previousSharedTag1ImageStatus,
                                Clone(sharedTag1ImageStatus, StageStatus.Succeeded),
                                sharedTag2ImageStatus,
                            }
                        },
                        new ImageResult
                        {
                            Digest = manifestDigest1,
                            Value = new List<ImageStatus>
                            {
                                previousSharedTag1ImageStatus,
                                Clone(sharedTag1ImageStatus, StageStatus.Succeeded),
                                Clone(sharedTag2ImageStatus, StageStatus.Succeeded),
                            }
                        }
                    }.GetEnumerator()
                },
                {
                    ImageName.Parse(platformDigest1).Digest,
                    new List<ImageResult>
                    {
                        new ImageResult
                        {
                            Digest = platformDigest1,
                            Value = new List<ImageStatus>
                            {
                                platformTag1ImageStatus,
                                platformTag2ImageStatus,
                            }
                        },
                        new ImageResult
                        {
                            Digest = platformDigest1,
                            Value = new List<ImageStatus>
                            {
                                Clone(platformTag1ImageStatus, StageStatus.Succeeded),
                                Clone(platformTag2ImageStatus, StageStatus.Succeeded),
                            }
                        }
                    }.GetEnumerator()
                },
                {
                    ImageName.Parse(platformDigest2).Digest,
                    new List<ImageResult>
                    {
                        new ImageResult
                        {
                            Digest = platformDigest2,
                            Value = new List<ImageStatus>()
                        },
                        new ImageResult
                        {
                            Digest = platformDigest2,
                            Value = new List<ImageStatus>
                            {
                                platformTag3ImageStatus
                            }
                        }
                    }.GetEnumerator()
                }
            };
            
            statusClientMock
                .Setup(o => o.GetImageResultAsync(It.IsAny<string>()))
                .ReturnsAsync((string digest) =>
                {
                    IEnumerator<ImageResult> enumerator = imageResultMapping[digest];
                    if (enumerator.MoveNext())
                    {
                        return enumerator.Current;
                    }

                    return null;
                });

            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            WaitForMcrImageIngestionCommand command = new WaitForMcrImageIngestionCommand(
                Mock.Of<ILoggerService>(),
                CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object),
                environmentServiceMock.Object);

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string dockerfile1Path = CreateDockerfile("1.0/repo1/os", tempFolderContext);
            string dockerfile2Path = CreateDockerfile("1.0/repo2/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo(repo1,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1Path, new string[] { platformTag1, platformTag2 })
                        })),
                CreateRepo(repo2,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile2Path, new string[] { platformTag3 })
                        }))
            );

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    {
                        new RepoData
                        {
                            Repo = repo1,
                            Images =
                            {
                                new ImageData
                                {
                                    Manifest = new ManifestData
                                    {
                                        Digest = manifestDigest1,
                                        SharedTags = new List<string>
                                        {
                                            sharedTag1,
                                            sharedTag2
                                        }
                                    },
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfile1Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag1,
                                                platformTag2
                                            },
                                            digest: platformDigest1)
                                    }
                                }
                            }
                        }
                    },
                    {
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
                                            PathHelper.NormalizePath(dockerfile2Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag3
                                            },
                                            digest: platformDigest2)
                                    }
                                }
                            }
                        }
                    }
                }
            };

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.MinimumQueueTime = baselineTime;
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));
            command.LoadManifest();

            await command.ExecuteAsync();

            statusClientMock.Verify(o => o.GetImageResultAsync(ImageName.Parse(manifestDigest1, Dockerfile.DefaultEscapeChar).Digest), Times.Exactly(4));
            statusClientMock.Verify(o => o.GetImageResultAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest), Times.Exactly(2));
            statusClientMock.Verify(o => o.GetImageResultAsync(ImageName.Parse(platformDigest2, Dockerfile.DefaultEscapeChar).Digest), Times.Exactly(2));
            environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task PublishFailure()
        {
            DateTime baselineTime = DateTime.Now;
            const string manifestDigest1 = "repo@sha256:manifestDigest1";
            const string sharedTag1 = "sharedTag1";
            const string platformTag1 = "platformTag1";
            const string platformTag2 = "platformTag2";
            const string repo1 = "repo1";
            const string platformDigest1 = "repo@sha256:platformDigest1";
            const string onboardingRequestId1 = "onboardingRequestId1";
            const string onboardingRequestId2 = "onboardingRequestId2";

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();

            ImageStatus sharedTag1ImageStatus = new ImageStatus
            {
                Tag = sharedTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OnboardingRequestId = onboardingRequestId1,
                OverallStatus = StageStatus.NotStarted
            };

            ImageStatus platformTag1ImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag2ImageStatus = new ImageStatus
            {
                Tag = platformTag2,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OnboardingRequestId = onboardingRequestId2,
                OverallStatus = StageStatus.Processing
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new Dictionary<string, IEnumerator<ImageResult>>
            {
                {
                    ImageName.Parse(manifestDigest1).Digest,
                    new List<ImageResult>
                    {
                        new ImageResult
                        {
                            Digest = manifestDigest1,
                            Value = new List<ImageStatus>
                            {
                                sharedTag1ImageStatus,
                            }
                        },
                        new ImageResult
                        {
                            Digest = manifestDigest1,
                            Value = new List<ImageStatus>
                            {
                                Clone(sharedTag1ImageStatus, StageStatus.Processing),
                            }
                        },
                        new ImageResult
                        {
                            Digest = manifestDigest1,
                            Value = new List<ImageStatus>
                            {
                                Clone(sharedTag1ImageStatus, StageStatus.Failed),
                            }
                        }
                    }.GetEnumerator()
                },
                {
                    ImageName.Parse(platformDigest1).Digest,
                    new List<ImageResult>
                    {
                        new ImageResult
                        {
                            Digest = platformDigest1,
                            Value = new List<ImageStatus>
                            {
                                platformTag1ImageStatus,
                                platformTag2ImageStatus,
                            }
                        },
                        new ImageResult
                        {
                            Digest = platformDigest1,
                            Value = new List<ImageStatus>
                            {
                                Clone(platformTag1ImageStatus, StageStatus.Succeeded),
                                Clone(platformTag2ImageStatus, StageStatus.Failed),
                            }
                        }
                    }.GetEnumerator()
                }
            };

            statusClientMock
                .Setup(o => o.GetImageResultAsync(It.IsAny<string>()))
                .ReturnsAsync((string digest) =>
                {
                    IEnumerator<ImageResult> enumerator = imageResultMapping[digest];
                    if (enumerator.MoveNext())
                    {
                        return enumerator.Current;
                    }

                    return null;
                });

            statusClientMock
                .Setup(o => o.GetImageResultDetailedAsync(ImageName.Parse(manifestDigest1, Dockerfile.DefaultEscapeChar).Digest, onboardingRequestId1))
                .ReturnsAsync(new ImageResultDetailed
                {
                    CommitDigest = manifestDigest1,
                    OnboardingRequestId = onboardingRequestId1,
                    OverallStatus = StageStatus.Failed,
                    Tag = sharedTag1,
                    TargetRepository = repo1,
                    Substatus = new ImageSubstatus()
                });

            statusClientMock
                .Setup(o => o.GetImageResultDetailedAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest, onboardingRequestId2))
                .ReturnsAsync(new ImageResultDetailed
                {
                    CommitDigest = platformDigest1,
                    OnboardingRequestId = onboardingRequestId2,
                    OverallStatus = StageStatus.Failed,
                    Tag = platformTag2,
                    TargetRepository = repo1,
                    Substatus = new ImageSubstatus()
                });

            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            WaitForMcrImageIngestionCommand command = new WaitForMcrImageIngestionCommand(
                Mock.Of<ILoggerService>(),
                CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object),
                environmentServiceMock.Object);

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string dockerfile1Path = CreateDockerfile("1.0/repo1/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo(repo1,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1Path, new string[] { platformTag1, platformTag2 })
                        }))
            );

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    {
                        new RepoData
                        {
                            Repo = repo1,
                            Images =
                            {
                                new ImageData
                                {
                                    Manifest = new ManifestData
                                    {
                                        Digest = manifestDigest1,
                                        SharedTags = new List<string>
                                        {
                                            sharedTag1
                                        }
                                    },
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfile1Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag1,
                                                platformTag2
                                            },
                                            digest: platformDigest1)
                                    }
                                }
                            }
                        }
                    }
                }
            };

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.MinimumQueueTime = baselineTime;
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));
            command.LoadManifest();

            await command.ExecuteAsync();

            statusClientMock.Verify(o => o.GetImageResultAsync(ImageName.Parse(manifestDigest1, Dockerfile.DefaultEscapeChar).Digest), Times.Exactly(3));
            statusClientMock.Verify(o => o.GetImageResultAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest), Times.Exactly(2));
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(ImageName.Parse(manifestDigest1, Dockerfile.DefaultEscapeChar).Digest, onboardingRequestId1), Times.Once);
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest, onboardingRequestId2), Times.Once);
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest, It.Is<string>(val => val != onboardingRequestId2)), Times.Never);
            environmentServiceMock.Verify(o => o.Exit(1), Times.Once);
        }

        /// <summary>
        /// Tests the scenario where a given digest has been queued for onboarding multiple times and the minimum queue time isn't set
        /// properly to filter them to just one onboarding request.
        /// </summary>
        [Fact]
        public async Task OnboardingRequestsWithDuplicateDigest_Success()
        {
            DateTime baselineTime = DateTime.Now;
            const string platformTag1 = "platformTag1";
            const string repo1 = "repo1";
            const string platformDigest1 = "repo@sha256:platformDigest1";

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();

            ImageStatus platformTag1aImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag1bImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(2),
                OverallStatus = StageStatus.Processing
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new Dictionary<string, IEnumerator<ImageResult>>
            {
                {
                    ImageName.Parse(platformDigest1).Digest,
                    new List<ImageResult>
                    {
                        new ImageResult
                        {
                            Digest = platformDigest1,
                            Value = new List<ImageStatus>
                            {
                                platformTag1aImageStatus,
                                platformTag1bImageStatus,
                            }
                        },
                        new ImageResult
                        {
                            Digest = platformDigest1,
                            Value = new List<ImageStatus>
                            {
                                Clone(platformTag1aImageStatus, StageStatus.Succeeded),
                                Clone(platformTag1bImageStatus, StageStatus.Failed),
                            }
                        }
                    }.GetEnumerator()
                }
            };

            statusClientMock
                .Setup(o => o.GetImageResultAsync(It.IsAny<string>()))
                .ReturnsAsync((string digest) =>
                {
                    IEnumerator<ImageResult> enumerator = imageResultMapping[digest];
                    if (enumerator.MoveNext())
                    {
                        return enumerator.Current;
                    }

                    return null;
                });

            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            WaitForMcrImageIngestionCommand command = new WaitForMcrImageIngestionCommand(
                Mock.Of<ILoggerService>(),
                CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object),
                environmentServiceMock.Object);

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string dockerfile1Path = CreateDockerfile("1.0/repo1/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo(repo1,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1Path, new string[] { platformTag1 })
                        }))
            );

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
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
                                            PathHelper.NormalizePath(dockerfile1Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag1
                                            },
                                            digest: platformDigest1)
                                    }
                                }
                            }
                        }
                    }
                }
            };

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.MinimumQueueTime = baselineTime;
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));
            command.LoadManifest();

            await command.ExecuteAsync();

            statusClientMock.Verify(o => o.GetImageResultAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest), Times.Exactly(2));
            environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Tests the scenario where a given digest has been queued for onboarding multiple times and the minimum queue time isn't set
        /// properly to filter them to just one onboarding request.
        /// </summary>
        [Fact]
        public async Task OnboardingRequestsWithDuplicateDigest_Failed()
        {
            DateTime baselineTime = DateTime.Now;
            const string platformTag1 = "platformTag1";
            const string repo1 = "repo1";
            const string platformDigest1 = "repo@sha256:platformDigest1";
            const string tag1aOnboardingRequestId = "onboard request1";
            const string tag1bOnboardingRequestId = "onboard request2";

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();

            ImageStatus platformTag1aImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing,
                OnboardingRequestId = tag1aOnboardingRequestId
            };
            
            ImageStatus platformTag1bImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(2),
                OverallStatus = StageStatus.Processing,
                OnboardingRequestId = tag1bOnboardingRequestId
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new Dictionary<string, IEnumerator<ImageResult>>
            {
                {
                    ImageName.Parse(platformDigest1).Digest,
                    new List<ImageResult>
                    {
                        new ImageResult
                        {
                            Digest = platformDigest1,
                            Value = new List<ImageStatus>
                            {
                                platformTag1aImageStatus,
                                platformTag1bImageStatus,
                            }
                        },
                        new ImageResult
                        {
                            Digest = platformDigest1,
                            Value = new List<ImageStatus>
                            {
                                Clone(platformTag1aImageStatus, StageStatus.Failed),
                                platformTag1bImageStatus,
                            }
                        },
                        new ImageResult
                        {
                            Digest = platformDigest1,
                            Value = new List<ImageStatus>
                            {
                                Clone(platformTag1aImageStatus, StageStatus.Failed),
                                Clone(platformTag1bImageStatus, StageStatus.Failed),
                            }
                        }
                    }.GetEnumerator()
                }
            };

            statusClientMock
                .Setup(o => o.GetImageResultAsync(It.IsAny<string>()))
                .ReturnsAsync((string digest) =>
                {
                    IEnumerator<ImageResult> enumerator = imageResultMapping[digest];
                    if (enumerator.MoveNext())
                    {
                        return enumerator.Current;
                    }

                    return null;
                });

            statusClientMock
                .Setup(o => o.GetImageResultDetailedAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest, tag1aOnboardingRequestId))
                .ReturnsAsync(new ImageResultDetailed
                {
                    CommitDigest = platformDigest1,
                    OnboardingRequestId = tag1aOnboardingRequestId,
                    OverallStatus = StageStatus.Failed,
                    Tag = platformTag1,
                    TargetRepository = repo1,
                    Substatus = new ImageSubstatus()
                });

            statusClientMock
                .Setup(o => o.GetImageResultDetailedAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest, tag1bOnboardingRequestId))
                .ReturnsAsync(new ImageResultDetailed
                {
                    CommitDigest = platformDigest1,
                    OnboardingRequestId = tag1bOnboardingRequestId,
                    OverallStatus = StageStatus.Failed,
                    Tag = platformTag1,
                    TargetRepository = repo1,
                    Substatus = new ImageSubstatus()
                });

            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            WaitForMcrImageIngestionCommand command = new WaitForMcrImageIngestionCommand(
                Mock.Of<ILoggerService>(),
                CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object),
                environmentServiceMock.Object);

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string dockerfile1Path = CreateDockerfile("1.0/repo1/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo(repo1,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1Path, new string[] { platformTag1 })
                        }))
            );

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
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
                                            PathHelper.NormalizePath(dockerfile1Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag1
                                            },
                                            digest: platformDigest1)
                                    }
                                }
                            }
                        }
                    }
                }
            };

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.MinimumQueueTime = baselineTime;
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));
            command.LoadManifest();

            await command.ExecuteAsync();

            statusClientMock.Verify(o => o.GetImageResultAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest), Times.Exactly(3));
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest, tag1aOnboardingRequestId), Times.Once);
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(ImageName.Parse(platformDigest1, Dockerfile.DefaultEscapeChar).Digest, tag1bOnboardingRequestId), Times.Once);
            environmentServiceMock.Verify(o => o.Exit(1), Times.Once);
        }

        /// <summary>
        /// Tests that the command times out if the publishing takes too long.
        /// </summary>
        [Fact]
        public async Task WaitTimeout()
        {
            DateTime baselineTime = DateTime.Now;
            const string platformTag1 = "platformTag1";
            const string repo1 = "repo1";
            const string platformDigest1 = "repo@sha256:platformDigest1";

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();
            statusClientMock
                .Setup(o => o.GetImageResultAsync(It.IsAny<string>()))
                .ReturnsAsync(
                    new ImageResult
                    {
                        Digest = platformDigest1,
                        Value = new List<ImageStatus>
                        {
                            new ImageStatus
                            {
                                Tag = platformTag1,
                                TargetRepository = repo1,
                                QueueTime = baselineTime.AddHours(1),
                                OverallStatus = StageStatus.Processing
                            }
                        }
                    });

            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            WaitForMcrImageIngestionCommand command = new WaitForMcrImageIngestionCommand(
                Mock.Of<ILoggerService>(),
                CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object),
                environmentServiceMock.Object);

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string dockerfile1Path = CreateDockerfile("1.0/repo1/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo(repo1,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1Path, new string[] { platformTag1 })
                        }))
            );

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
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
                                            PathHelper.NormalizePath(dockerfile1Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag1
                                            },
                                            digest: platformDigest1)
                                    }
                                }
                            }
                        }
                    }
                }
            };

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.MinimumQueueTime = baselineTime;
            command.Options.WaitTimeout = TimeSpan.FromSeconds(3);

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));
            command.LoadManifest();

            await Assert.ThrowsAsync<TimeoutException>(() => command.ExecuteAsync());
        }

        [Fact]
        public async Task SyndicatedTags()
        {
            DateTime baselineTime = DateTime.Now;
            const string registry = "mcr.microsoft.com";
            string repo1ManifestDigest1 = $"{registry}/repo1@sha256:123";
            string repo2ManifestDigest1 = $"{registry}/repo2@sha256:123";
            const string sharedTag1 = "sharedTag1";
            const string platformTag1 = "platformTag1";
            const string repo1 = "repo1";
            const string repo2 = "repo2";
            string repo1PlatformDigest1 = $"{registry}/repo1@sha256:abc";
            string repo2PlatformDigest1 = $"{registry}/repo2@sha256:abc";

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();

            ImageStatus repo1SharedTag1ImageStatus = new ImageStatus
            {
                Tag = sharedTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Succeeded
            };

            ImageStatus repo2SharedTag1ImageStatus = new ImageStatus
            {
                Tag = sharedTag1,
                TargetRepository = repo2,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Succeeded
            };

            ImageStatus repo1PlatformTag1ImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                TargetRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Succeeded
            };

            ImageStatus repo2PlatformTag1aImageStatus = new ImageStatus
            {
                Tag = $"{platformTag1}a",
                TargetRepository = repo2,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Succeeded
            };

            ImageStatus repo2PlatformTag1bImageStatus = new ImageStatus
            {
                Tag = $"{platformTag1}b",
                TargetRepository = repo2,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Succeeded
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new Dictionary<string, IEnumerator<ImageResult>>
            {
                {
                    ImageName.Parse(repo1ManifestDigest1).Digest,
                    new List<ImageResult>
                    {
                        new ImageResult
                        {
                            Value = new List<ImageStatus>
                            {
                                repo1SharedTag1ImageStatus
                            }
                        },
                        new ImageResult
                        {
                            Value = new List<ImageStatus>
                            {
                                repo2SharedTag1ImageStatus
                            }
                        }
                    }.GetEnumerator()
                },
                {
                    ImageName.Parse(repo1PlatformDigest1).Digest,
                    new List<ImageResult>
                    {
                        new ImageResult
                        {
                            Value = new List<ImageStatus>
                            {
                                repo1PlatformTag1ImageStatus
                            }
                        },
                        new ImageResult
                        {
                            Value = new List<ImageStatus>
                            {
                                repo2PlatformTag1aImageStatus
                            }
                        },
                        new ImageResult
                        {
                            Value = new List<ImageStatus>
                            {
                                repo2PlatformTag1bImageStatus
                            }
                        }
                    }.GetEnumerator()
                }
            };

            statusClientMock
                .Setup(o => o.GetImageResultAsync(It.IsAny<string>()))
                .ReturnsAsync((string digest) =>
                {
                    IEnumerator<ImageResult> enumerator = imageResultMapping[digest];
                    if (enumerator.MoveNext())
                    {
                        return enumerator.Current;
                    }

                    return null;
                });

            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            WaitForMcrImageIngestionCommand command = new WaitForMcrImageIngestionCommand(
                Mock.Of<ILoggerService>(),
                CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object),
                environmentServiceMock.Object);

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string dockerfile1Path = CreateDockerfile("1.0/repo1/os", tempFolderContext);

            const string syndicatedRepo = "repo2";

            Manifest manifest = CreateManifest(
                CreateRepo(repo1,
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfile1Path, new string[] { platformTag1 })
                        },
                        sharedTags: new Dictionary<string, Tag>
                        {
                            {
                                sharedTag1,
                                new Tag
                                {
                                    Syndication = new TagSyndication
                                    {
                                        Repo = syndicatedRepo,
                                        DestinationTags = new string[0]
                                    }
                                }
                            }
                        }))
            );
            manifest.Registry = registry;

            Platform platform = manifest.Repos.First().Images.First().Platforms.First();
            platform.Tags[platformTag1].Syndication = new TagSyndication
            {
                Repo = syndicatedRepo,
                DestinationTags = new string[]
                {
                    $"{platformTag1}a",
                    $"{platformTag1}b"
                }
            };

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    {
                        new RepoData
                        {
                            Repo = repo1,
                            Images =
                            {
                                new ImageData
                                {
                                    Manifest = new ManifestData
                                    {
                                        Digest = repo1ManifestDigest1,
                                        SharedTags = new List<string>
                                        {
                                            sharedTag1
                                        }
                                    },
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfile1Path),
                                            simpleTags: new List<string>
                                            {
                                                platformTag1
                                            },
                                            digest: repo1PlatformDigest1)
                                    }
                                }
                            }
                        }
                    }
                }
            };

            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.MinimumQueueTime = baselineTime;
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));
            command.LoadManifest();

            await command.ExecuteAsync();

            statusClientMock.Verify(o => o.GetImageResultAsync(ImageName.Parse(repo1ManifestDigest1, Dockerfile.DefaultEscapeChar).Digest), Times.Exactly(2));
            statusClientMock.Verify(o => o.GetImageResultAsync(ImageName.Parse(repo1PlatformDigest1, Dockerfile.DefaultEscapeChar).Digest), Times.Exactly(3));
            environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
        }

        private static ImageStatus Clone(ImageStatus status, StageStatus? newOverallStatusValue) =>
            new ImageStatus
            {
                OnboardingRequestId = status.OnboardingRequestId,
                QueueTime = status.QueueTime,
                SourceRepository = status.SourceRepository,
                TargetRepository = status.TargetRepository,
                Tag = status.Tag,
                OverallStatus = newOverallStatusValue ?? status.OverallStatus
            };

        private static IMcrStatusClientFactory CreateMcrStatusClientFactory(
            string tenant, string clientId, string clientSecret, IMcrStatusClient statusClient)
        {
            Mock<IMcrStatusClientFactory> mock = new Mock<IMcrStatusClientFactory>();
            mock
                .Setup(o => o.Create(tenant, clientId, clientSecret))
                .Returns(statusClient);
            return mock.Object;
        }
    }
}
