// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.ImageBuilder;
using Microsoft.DotNet.DockerTools.ImageBuilder.Commands;
using Microsoft.DotNet.DockerTools.ImageBuilder.Models.McrStatus;
using Moq;
using Xunit;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Tests
{
    public class WaitForMcrDocIngestionCommandTests
    {
        [Fact]
        public async Task SuccessfulPublish()
        {
            const string commitDigest = "commit digest";

            IEnumerator<CommitResult> commitResultEnumerator = new List<CommitResult>
            {
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            OverallStatus = StageStatus.NotStarted
                        }
                    }
                },
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            OverallStatus = StageStatus.Processing
                        }
                    }
                },
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            OverallStatus = StageStatus.Processing
                        }
                    }
                },
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            OverallStatus = StageStatus.Succeeded
                        }
                    }
                }
            }.GetEnumerator();

            Mock<IMcrStatusClient> statusClientMock = new();
            statusClientMock
                .Setup(o => o.GetCommitResultAsync(commitDigest))
                .ReturnsAsync(() =>
                {
                    if (commitResultEnumerator.MoveNext())
                    {
                        return commitResultEnumerator.Current;
                    }

                    return null;
                });

            Mock<IEnvironmentService> environmentServiceMock = new();

            WaitForMcrDocIngestionCommand command = new(
                Mock.Of<ILoggerService>(),
                statusClientMock.Object,
                environmentServiceMock.Object);

            command.Options.CommitDigest = commitDigest;
            command.Options.IngestionOptions.WaitTimeout = TimeSpan.FromMinutes(1);

            await command.ExecuteAsync();

            environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
            statusClientMock.Verify(o => o.GetCommitResultAsync(commitDigest), Times.Exactly(4));
        }

        [Fact]
        public async Task PublishFailure()
        {
            const string commitDigest = "commit digest";
            const string onboardingRequestId = "onboardingRequestId";

            IEnumerator<CommitResult> commitResultEnumerator = new List<CommitResult>
            {
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            OverallStatus = StageStatus.NotStarted
                        }
                    }
                },
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            OverallStatus = StageStatus.Processing
                        }
                    }
                },
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            OverallStatus = StageStatus.Processing
                        }
                    }
                },
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            OnboardingRequestId = onboardingRequestId,
                            OverallStatus = StageStatus.Failed
                        }
                    }
                }
            }.GetEnumerator();

            Mock<IMcrStatusClient> statusClientMock = new();
            statusClientMock
                .Setup(o => o.GetCommitResultAsync(commitDigest))
                .ReturnsAsync(() =>
                {
                    if (commitResultEnumerator.MoveNext())
                    {
                        return commitResultEnumerator.Current;
                    }

                    return null;
                });

            statusClientMock
                .Setup(o => o.GetCommitResultDetailedAsync(commitDigest, onboardingRequestId))
                .ReturnsAsync(new CommitResultDetailed());

            Mock<IEnvironmentService> environmentServiceMock = new();
            Exception exitException = new();
            environmentServiceMock
                .Setup(o => o.Exit(1))
                .Throws(exitException);

            WaitForMcrDocIngestionCommand command = new WaitForMcrDocIngestionCommand(
                Mock.Of<ILoggerService>(),
                statusClientMock.Object,
                environmentServiceMock.Object);

            command.Options.CommitDigest = commitDigest;
            command.Options.IngestionOptions.WaitTimeout = TimeSpan.FromMinutes(1);

            Exception actualException = await Assert.ThrowsAsync<Exception>(command.ExecuteAsync);

            Assert.Same(exitException, actualException);
            statusClientMock.Verify(o => o.GetCommitResultAsync(commitDigest), Times.Exactly(4));
            statusClientMock.Verify(o => o.GetCommitResultDetailedAsync(commitDigest, onboardingRequestId), Times.Once);
        }

        /// <summary>
        /// Tests the scenario where a given digest has been queued for onboarding multiple times.
        /// </summary>
        [Fact]
        public async Task OnboardingRequestsWithDuplicateDigest_Success()
        {
            const string commitDigest = "commit digest";
            const string onboardingRequestId = "onboard";

            DateTime baselineTime = DateTime.Now;

            IEnumerator<CommitResult> commitResultEnumerator = new List<CommitResult>
            {
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            QueueTime = baselineTime,
                            OverallStatus = StageStatus.Processing
                        },
                        new CommitStatus
                        {
                            QueueTime = baselineTime.AddMinutes(1),
                            OverallStatus = StageStatus.Processing
                        }
                    }
                },
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            QueueTime = baselineTime,
                            OverallStatus = StageStatus.Processing
                        },
                        new CommitStatus
                        {
                            OnboardingRequestId = onboardingRequestId,
                            QueueTime = baselineTime.AddMinutes(1),
                            OverallStatus = StageStatus.Failed
                        }
                    }
                },
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            QueueTime = baselineTime,
                            OverallStatus = StageStatus.Succeeded
                        },
                        new CommitStatus
                        {
                            OnboardingRequestId = onboardingRequestId,
                            QueueTime = baselineTime.AddMinutes(1),
                            OverallStatus = StageStatus.Failed
                        }
                    }
                }
            }.GetEnumerator();

            Mock<IMcrStatusClient> statusClientMock = new();
            statusClientMock
                .Setup(o => o.GetCommitResultAsync(commitDigest))
                .ReturnsAsync(() =>
                {
                    if (commitResultEnumerator.MoveNext())
                    {
                        return commitResultEnumerator.Current;
                    }

                    return null;
                });

            statusClientMock
                .Setup(o => o.GetCommitResultDetailedAsync(commitDigest, onboardingRequestId))
                .ReturnsAsync(new CommitResultDetailed());

            Mock<IEnvironmentService> environmentServiceMock = new();

            WaitForMcrDocIngestionCommand command = new(
                Mock.Of<ILoggerService>(),
                statusClientMock.Object,
                environmentServiceMock.Object);

            command.Options.CommitDigest = commitDigest;
            command.Options.IngestionOptions.WaitTimeout = TimeSpan.FromMinutes(1);

            await command.ExecuteAsync();

            environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
            statusClientMock.Verify(o => o.GetCommitResultAsync(commitDigest), Times.Exactly(3));
        }

        /// <summary>
        /// Tests the scenario where a given digest has been queued for onboarding multiple times.
        /// </summary>
        [Fact]
        public async Task OnboardingRequestsWithDuplicateDigest_Failed()
        {
            const string commitDigest = "commit digest";
            const string onboardingRequestId1 = "onboard1";
            const string onboardingRequestId2 = "onboard1";

            DateTime baselineTime = DateTime.Now;

            IEnumerator<CommitResult> commitResultEnumerator = new List<CommitResult>
            {
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            QueueTime = baselineTime,
                            OverallStatus = StageStatus.Processing
                        },
                        new CommitStatus
                        {
                            QueueTime = baselineTime.AddMinutes(1),
                            OverallStatus = StageStatus.Processing
                        }
                    }
                },
                new CommitResult
                {
                    Value = new List<CommitStatus>
                    {
                        new CommitStatus
                        {
                            OnboardingRequestId = onboardingRequestId1,
                            QueueTime = baselineTime,
                            OverallStatus = StageStatus.Failed
                        },
                        new CommitStatus
                        {
                            OnboardingRequestId = onboardingRequestId2,
                            QueueTime = baselineTime.AddMinutes(1),
                            OverallStatus = StageStatus.Failed
                        }
                    }
                }
            }.GetEnumerator();

            Mock<IMcrStatusClient> statusClientMock = new();
            statusClientMock
                .Setup(o => o.GetCommitResultAsync(commitDigest))
                .ReturnsAsync(() =>
                {
                    if (commitResultEnumerator.MoveNext())
                    {
                        return commitResultEnumerator.Current;
                    }

                    return null;
                });

            statusClientMock
                .Setup(o => o.GetCommitResultDetailedAsync(commitDigest, onboardingRequestId1))
                .ReturnsAsync(new CommitResultDetailed());
            statusClientMock
                .Setup(o => o.GetCommitResultDetailedAsync(commitDigest, onboardingRequestId2))
                .ReturnsAsync(new CommitResultDetailed());

            Mock<IEnvironmentService> environmentServiceMock = new();
            Exception exitException = new();
            environmentServiceMock
                .Setup(o => o.Exit(1))
                .Throws(exitException);

            WaitForMcrDocIngestionCommand command = new WaitForMcrDocIngestionCommand(
                Mock.Of<ILoggerService>(),
                statusClientMock.Object,
                environmentServiceMock.Object);

            command.Options.CommitDigest = commitDigest;
            command.Options.IngestionOptions.WaitTimeout = TimeSpan.FromMinutes(1);

            Exception actualException = await Assert.ThrowsAsync<Exception>(command.ExecuteAsync);

            Assert.Same(exitException, actualException);
            environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Once);
            statusClientMock.Verify(o => o.GetCommitResultAsync(commitDigest), Times.Exactly(2));
        }

        /// <summary>
        /// Tests that the command times out if the publishing takes too long.
        /// </summary>
        [Fact]
        public async Task WaitTimeout()
        {
            const string commitDigest = "commit digest";

            Mock<IMcrStatusClient> statusClientMock = new();
            statusClientMock
                .Setup(o => o.GetCommitResultAsync(commitDigest))
                .ReturnsAsync(
                    new CommitResult
                    {
                        Value = new List<CommitStatus>
                        {
                            new CommitStatus
                            {
                                OverallStatus = StageStatus.NotStarted
                            }
                        }
                    });

            Mock<IEnvironmentService> environmentServiceMock = new();

            WaitForMcrDocIngestionCommand command = new(
                Mock.Of<ILoggerService>(),
                statusClientMock.Object,
                environmentServiceMock.Object);

            command.Options.CommitDigest = commitDigest;
            command.Options.IngestionOptions.WaitTimeout = TimeSpan.FromSeconds(3);

            environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
            await Assert.ThrowsAsync<TimeoutException>(command.ExecuteAsync);
        }
    }
}
