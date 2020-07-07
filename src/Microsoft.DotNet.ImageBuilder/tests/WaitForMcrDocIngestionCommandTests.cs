// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class WaitForMcrDocIngestionCommandTests
    {
        [Fact]
        public async Task SuccessfulPublish()
        {
            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";

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

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();
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

            IMcrStatusClientFactory statusClientFactory = CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object);

            WaitForMcrDocIngestionCommand command = new WaitForMcrDocIngestionCommand(
                Mock.Of<ILoggerService>(),
                statusClientFactory);

            command.Options.CommitDigest = commitDigest;
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);

            await command.ExecuteAsync();

            statusClientMock.Verify(o => o.GetCommitResultAsync(commitDigest), Times.Exactly(4));
        }

        [Fact]
        public async Task PublishFailure()
        {
            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";
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

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();
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

            IMcrStatusClientFactory statusClientFactory = CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object);

            WaitForMcrDocIngestionCommand command = new WaitForMcrDocIngestionCommand(
                Mock.Of<ILoggerService>(),
                statusClientFactory);

            command.Options.CommitDigest = commitDigest;
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);

            await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());

            statusClientMock.Verify(o => o.GetCommitResultAsync(commitDigest), Times.Exactly(4));
            statusClientMock.Verify(o => o.GetCommitResultDetailedAsync(commitDigest, onboardingRequestId), Times.Once);
        }

        /// <summary>
        /// Tests the scenario where a given digest has been queued for onboarding multiple times.
        /// </summary>
        [Fact]
        public async Task OnboardingRequestsWithDuplicateDigest()
        {
            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";

            const string commitDigest = "commit digest";

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
                            OverallStatus = StageStatus.Succeeded
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
                            OverallStatus = StageStatus.Succeeded
                        },
                        new CommitStatus
                        {
                            QueueTime = baselineTime.AddMinutes(1),
                            OverallStatus = StageStatus.Succeeded
                        }
                    }
                }
            }.GetEnumerator();

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();
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

            IMcrStatusClientFactory statusClientFactory = CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object);

            WaitForMcrDocIngestionCommand command = new WaitForMcrDocIngestionCommand(
                Mock.Of<ILoggerService>(),
                statusClientFactory);

            command.Options.CommitDigest = commitDigest;
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.WaitTimeout = TimeSpan.FromMinutes(1);

            await command.ExecuteAsync();

            statusClientMock.Verify(o => o.GetCommitResultAsync(commitDigest), Times.Exactly(2));
        }

        /// <summary>
        /// Tests that the command times out if the publishing takes too long.
        /// </summary>
        [Fact]
        public async Task WaitTimeout()
        {
            const string tenant = "my tenant";
            const string clientId = "my id";
            const string clientSecret = "very secret";
            const string commitDigest = "commit digest";

            Mock<IMcrStatusClient> statusClientMock = new Mock<IMcrStatusClient>();
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

            IMcrStatusClientFactory statusClientFactory = CreateMcrStatusClientFactory(tenant, clientId, clientSecret, statusClientMock.Object);

            WaitForMcrDocIngestionCommand command = new WaitForMcrDocIngestionCommand(
                Mock.Of<ILoggerService>(),
                statusClientFactory);

            command.Options.CommitDigest = commitDigest;
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ServicePrincipal.ClientId = clientId;
            command.Options.ServicePrincipal.Secret = clientSecret;
            command.Options.WaitTimeout = TimeSpan.FromSeconds(3);

            await Assert.ThrowsAsync<TimeoutException>(() => command.ExecuteAsync());
        }

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
