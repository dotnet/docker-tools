#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.MarStatusHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class MarImageIngestionReporterTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("public/")]
        [InlineData("internal/private/")]
        public async Task SuccessfulPublish(string repoPrefix)
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

            var statusClientMock = new Mock<IMcrStatusClient>();
            var statusClientFactoryMock = CreateMarStatusClientFactoryMock(statusClientMock.Object);

            ImageStatus previousSharedTag1ImageStatus = new ImageStatus
            {
                Tag = sharedTag1,
                SourceRepository = repoPrefix + repo1,
                QueueTime = baselineTime.AddHours(-1),
                OverallStatus = StageStatus.Succeeded
            };

            ImageStatus sharedTag1ImageStatus = new ImageStatus
            {
                Tag = sharedTag1,
                SourceRepository = repoPrefix + repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.NotStarted
            };

            ImageStatus sharedTag2ImageStatus = new ImageStatus
            {
                Tag = sharedTag2,
                SourceRepository = repoPrefix + repo1,
                QueueTime = baselineTime,
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag1ImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                SourceRepository = repoPrefix + repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag2ImageStatus = new ImageStatus
            {
                Tag = platformTag2,
                SourceRepository = repoPrefix + repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag3ImageStatus = new ImageStatus
            {
                Tag = platformTag3,
                SourceRepository = repoPrefix + repo2,
                QueueTime = baselineTime.AddSeconds(1),
                OverallStatus = StageStatus.Succeeded
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new Dictionary<string, IEnumerator<ImageResult>>
            {
                {
                    DockerHelper.GetDigestSha(manifestDigest1),
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
                    DockerHelper.GetDigestSha(platformDigest1),
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
                    DockerHelper.GetDigestSha(platformDigest2),
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

            Mock<IEnvironmentService> environmentServiceMock = new();

            MarImageIngestionReporter reporter = new(
                Mock.Of<ILogger<MarImageIngestionReporter>>(),
                statusClientFactoryMock.Object,
                environmentServiceMock.Object);

            List<DigestInfo> digestInfos =
                [
                    new DigestInfo(DockerHelper.GetDigestSha(manifestDigest1), repoPrefix + repo1, [sharedTag1, sharedTag2]),
                    new DigestInfo(DockerHelper.GetDigestSha(platformDigest1), repoPrefix + repo1, [platformTag1, platformTag2]),
                    new DigestInfo(DockerHelper.GetDigestSha(platformDigest2), repoPrefix + repo2, [platformTag3])
                ];

            await reporter.ReportImageStatusesAsync(
                Mock.Of<IServiceConnection>(),
                digestInfos,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMicroseconds(1),
                baselineTime);

            statusClientMock.Verify(o => o.GetImageResultAsync(DockerHelper.GetDigestSha(manifestDigest1)), Times.Exactly(4));
            statusClientMock.Verify(o => o.GetImageResultAsync(DockerHelper.GetDigestSha(platformDigest1)), Times.Exactly(2));
            statusClientMock.Verify(o => o.GetImageResultAsync(DockerHelper.GetDigestSha(platformDigest2)), Times.Exactly(2));
            environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SuccessfulPublish_TaglessDigest()
        {
            DateTime baselineTime = DateTime.Now;
            const string digest = "repo@sha256:digest";
            const string repo1 = "repo1";

            Mock<IMcrStatusClient> statusClientMock = new();
            var statusClientFactoryMock = CreateMarStatusClientFactoryMock(statusClientMock.Object);

            ImageStatus sharedTag2ImageStatus = new()
            {
                Tag = null,
                SourceRepository = repo1,
                QueueTime = baselineTime,
                OverallStatus = StageStatus.NotStarted
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new()
            {
                {
                    DockerHelper.GetDigestSha(digest),
                    new List<ImageResult>
                    {
                        new() {
                            Digest = digest,
                            Value =
                            [
                                sharedTag2ImageStatus,
                            ]
                        },
                        new() {
                            Digest = digest,
                            Value =
                            [
                                Clone(sharedTag2ImageStatus, StageStatus.Processing),
                            ]
                        },
                        new() {
                            Digest = digest,
                            Value =
                            [
                                Clone(sharedTag2ImageStatus, StageStatus.Succeeded),
                            ]
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

            Mock<IEnvironmentService> environmentServiceMock = new();

            MarImageIngestionReporter reporter = new(
                Mock.Of<ILogger<MarImageIngestionReporter>>(),
                statusClientFactoryMock.Object,
                environmentServiceMock.Object);

            List<DigestInfo> digestInfos =
                [
                    new(DockerHelper.GetDigestSha(digest), repo1, []),
                ];

            await reporter.ReportImageStatusesAsync(
                Mock.Of<IServiceConnection>(),
                digestInfos,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMicroseconds(1),
                baselineTime);

            statusClientMock.Verify(o => o.GetImageResultAsync(DockerHelper.GetDigestSha(digest)), Times.Exactly(3));
            environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task PublishFailure_TaglessDigest()
        {
            DateTime baselineTime = DateTime.Now;
            const string repo1 = "repo1";
            const string digest = "repo@sha256:digest";
            const string onboardingRequestId = "onboardingRequestId";

            Mock<IMcrStatusClient> statusClientMock = new();
            var statusClientFactoryMock = CreateMarStatusClientFactoryMock(statusClientMock.Object);

            ImageStatus platformTag2ImageStatus = new()
            {
                Tag = null,
                SourceRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OnboardingRequestId = onboardingRequestId,
                OverallStatus = StageStatus.Processing
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new()
            {
                {
                    DockerHelper.GetDigestSha(digest),
                    new List<ImageResult>
                    {
                        new() {
                            Digest = digest,
                            Value =
                            [
                                platformTag2ImageStatus,
                            ]
                        },
                        new() {
                            Digest = digest,
                            Value =
                            [
                                Clone(platformTag2ImageStatus, StageStatus.Failed),
                            ]
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
                .Setup(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(digest), onboardingRequestId))
                .ReturnsAsync(new ImageResultDetailed
                {
                    CommitDigest = digest,
                    OnboardingRequestId = onboardingRequestId,
                    OverallStatus = StageStatus.Failed,
                    Tag = null,
                    SourceRepository = repo1,
                    Substatus = new ImageSubstatus()
                });

            Mock<IEnvironmentService> environmentServiceMock = new();

            MarImageIngestionReporter reporter = new(
                Mock.Of<ILogger<MarImageIngestionReporter>>(),
                statusClientFactoryMock.Object,
                environmentServiceMock.Object);

            List<DigestInfo> digestInfos =
                [
                    new(DockerHelper.GetDigestSha(digest), repo1, []),
                ];

            await reporter.ReportImageStatusesAsync(
                Mock.Of<IServiceConnection>(),
                digestInfos,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMicroseconds(1),
                baselineTime);

            statusClientMock.Verify(o => o.GetImageResultAsync(DockerHelper.GetDigestSha(digest)), Times.Exactly(2));
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(digest), onboardingRequestId), Times.Once);
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(digest), It.Is<string>(val => val != onboardingRequestId)), Times.Never);
            environmentServiceMock.Verify(o => o.Exit(1), Times.Once);
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

            Mock<IMcrStatusClient> statusClientMock = new();
            var statusClientFactoryMock = CreateMarStatusClientFactoryMock(statusClientMock.Object);

            ImageStatus sharedTag1ImageStatus = new ImageStatus
            {
                Tag = sharedTag1,
                SourceRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OnboardingRequestId = onboardingRequestId1,
                OverallStatus = StageStatus.NotStarted
            };

            ImageStatus platformTag1ImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                SourceRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag2ImageStatus = new ImageStatus
            {
                Tag = platformTag2,
                SourceRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OnboardingRequestId = onboardingRequestId2,
                OverallStatus = StageStatus.Processing
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new Dictionary<string, IEnumerator<ImageResult>>
            {
                {
                    DockerHelper.GetDigestSha(manifestDigest1),
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
                    DockerHelper.GetDigestSha(platformDigest1),
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
                .Setup(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(manifestDigest1), onboardingRequestId1))
                .ReturnsAsync(new ImageResultDetailed
                {
                    CommitDigest = manifestDigest1,
                    OnboardingRequestId = onboardingRequestId1,
                    OverallStatus = StageStatus.Failed,
                    Tag = sharedTag1,
                    SourceRepository = repo1,
                    Substatus = new ImageSubstatus()
                });

            statusClientMock
                .Setup(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(platformDigest1), onboardingRequestId2))
                .ReturnsAsync(new ImageResultDetailed
                {
                    CommitDigest = platformDigest1,
                    OnboardingRequestId = onboardingRequestId2,
                    OverallStatus = StageStatus.Failed,
                    Tag = platformTag2,
                    SourceRepository = repo1,
                    Substatus = new ImageSubstatus()
                });

            Mock<IEnvironmentService> environmentServiceMock = new();

            MarImageIngestionReporter reporter = new(
                Mock.Of<ILogger<MarImageIngestionReporter>>(),
                statusClientFactoryMock.Object,
                environmentServiceMock.Object);

            List<DigestInfo> digestInfos =
                [
                    new DigestInfo(DockerHelper.GetDigestSha(manifestDigest1), repo1, [sharedTag1]),
                    new DigestInfo(DockerHelper.GetDigestSha(platformDigest1), repo1, [platformTag1, platformTag2]),
                ];

            await reporter.ReportImageStatusesAsync(
                Mock.Of<IServiceConnection>(),
                digestInfos,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMicroseconds(1),
                baselineTime);

            statusClientMock.Verify(o => o.GetImageResultAsync(DockerHelper.GetDigestSha(manifestDigest1)), Times.Exactly(3));
            statusClientMock.Verify(o => o.GetImageResultAsync(DockerHelper.GetDigestSha(platformDigest1)), Times.Exactly(2));
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(manifestDigest1), onboardingRequestId1), Times.Once);
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(platformDigest1), onboardingRequestId2), Times.Once);
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(platformDigest1), It.Is<string>(val => val != onboardingRequestId2)), Times.Never);
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

            Mock<IMcrStatusClient> statusClientMock = new();
            var statusClientFactoryMock = CreateMarStatusClientFactoryMock(statusClientMock.Object);

            ImageStatus platformTag1aImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                SourceRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing
            };

            ImageStatus platformTag1bImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                SourceRepository = repo1,
                QueueTime = baselineTime.AddHours(2),
                OverallStatus = StageStatus.Processing
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new Dictionary<string, IEnumerator<ImageResult>>
            {
                {
                    DockerHelper.GetDigestSha(platformDigest1),
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

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            MarImageIngestionReporter reporter = new(
                Mock.Of<ILogger<MarImageIngestionReporter>>(),
                statusClientFactoryMock.Object,
                environmentServiceMock.Object);

            List<DigestInfo> digestInfos =
                [
                    new DigestInfo(DockerHelper.GetDigestSha(platformDigest1), repo1, [platformTag1]),
                ];

            await reporter.ReportImageStatusesAsync(
                Mock.Of<IServiceConnection>(),
                digestInfos,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMicroseconds(1),
                baselineTime);

            statusClientMock.Verify(o => o.GetImageResultAsync(DockerHelper.GetDigestSha(platformDigest1)), Times.Exactly(2));
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

            Mock<IMcrStatusClient> statusClientMock = new();
            var statusClientFactoryMock = CreateMarStatusClientFactoryMock(statusClientMock.Object);

            ImageStatus platformTag1aImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                SourceRepository = repo1,
                QueueTime = baselineTime.AddHours(1),
                OverallStatus = StageStatus.Processing,
                OnboardingRequestId = tag1aOnboardingRequestId
            };

            ImageStatus platformTag1bImageStatus = new ImageStatus
            {
                Tag = platformTag1,
                SourceRepository = repo1,
                QueueTime = baselineTime.AddHours(2),
                OverallStatus = StageStatus.Processing,
                OnboardingRequestId = tag1bOnboardingRequestId
            };

            Dictionary<string, IEnumerator<ImageResult>> imageResultMapping = new Dictionary<string, IEnumerator<ImageResult>>
            {
                {
                    DockerHelper.GetDigestSha(platformDigest1),
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
                .Setup(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(platformDigest1), tag1aOnboardingRequestId))
                .ReturnsAsync(new ImageResultDetailed
                {
                    CommitDigest = platformDigest1,
                    OnboardingRequestId = tag1aOnboardingRequestId,
                    OverallStatus = StageStatus.Failed,
                    Tag = platformTag1,
                    SourceRepository = repo1,
                    Substatus = new ImageSubstatus()
                });

            statusClientMock
                .Setup(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(platformDigest1), tag1bOnboardingRequestId))
                .ReturnsAsync(new ImageResultDetailed
                {
                    CommitDigest = platformDigest1,
                    OnboardingRequestId = tag1bOnboardingRequestId,
                    OverallStatus = StageStatus.Failed,
                    Tag = platformTag1,
                    SourceRepository = repo1,
                    Substatus = new ImageSubstatus()
                });

            Mock<IEnvironmentService> environmentServiceMock = new();

            MarImageIngestionReporter reporter = new(
                Mock.Of<ILogger<MarImageIngestionReporter>>(),
                statusClientFactoryMock.Object,
                environmentServiceMock.Object);

            List<DigestInfo> digestInfos =
                [
                    new DigestInfo(DockerHelper.GetDigestSha(platformDigest1), repo1, [platformTag1]),
                ];

            await reporter.ReportImageStatusesAsync(
                Mock.Of<IServiceConnection>(),
                digestInfos,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMicroseconds(1),
                baselineTime);

            statusClientMock.Verify(o => o.GetImageResultAsync(DockerHelper.GetDigestSha(platformDigest1)), Times.Exactly(3));
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(platformDigest1), tag1aOnboardingRequestId), Times.Once);
            statusClientMock.Verify(o => o.GetImageResultDetailedAsync(DockerHelper.GetDigestSha(platformDigest1), tag1bOnboardingRequestId), Times.Once);
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

            Mock<IMcrStatusClient> statusClientMock = new();
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
                                SourceRepository = repo1,
                                QueueTime = baselineTime.AddHours(1),
                                OverallStatus = StageStatus.Processing
                            }
                        }
                    });

            var environmentServiceMock = new Mock<IEnvironmentService>();
            var statusClientFactoryMock = CreateMarStatusClientFactoryMock(statusClientMock.Object);

            MarImageIngestionReporter reporter = new(
                Mock.Of<ILogger<MarImageIngestionReporter>>(),
                statusClientFactoryMock.Object,
                environmentServiceMock.Object);

            List<DigestInfo> digestInfos =
                [
                    new DigestInfo(DockerHelper.GetDigestSha(platformDigest1), repo1, [platformTag1]),
                ];

            await Assert.ThrowsAsync<TimeoutException>(() => reporter.ReportImageStatusesAsync(
                Mock.Of<IServiceConnection>(),
                digestInfos,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMicroseconds(1),
                baselineTime));
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
    }
}
