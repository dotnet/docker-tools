// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class QueueBuildCommandTests
    {
        /// <summary>
        /// Verifies that no build is queued if a build is currently in progress.
        /// </summary>
        [Fact]
        public async Task QueueBuildCommand_BuildInProgress()
        {
            const string path1 = "path1";

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription("repo1")
            };

            List<Dictionary<string, List<string>>> pathData = new List<Dictionary<string, List<string>>>
            {
                new Dictionary<string, List<string>>
                {
                    {
                        subscriptions[0].Id,
                        new List<string>
                        {
                            path1
                        }
                    }
                }
            };

            using (TestContext context = new TestContext(subscriptions, pathData, hasInProgressBuild: true))
            {
                await context.ExecuteCommandAsync();

                // Normally this state would cause a build to be queued but since
                // a build is marked as in progress, it doesn't.

                context.Verify();
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued builds for two
        /// subscriptions that have image paths.
        /// </summary>
        [Fact]
        public async Task QueueBuildCommand_MultiSubscription()
        {
            const string path1 = "path1";
            const string path2 = "path2";
            const string path3 = "path3";

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription("repo1"),
                CreateSubscription("repo2")
            };

            List<Dictionary<string, List<string>>> pathData = new List<Dictionary<string, List<string>>>
            {
                new Dictionary<string, List<string>>
                {
                    {
                        subscriptions[0].Id,
                        new List<string>
                        {
                            path1
                        }
                    },
                    {
                        subscriptions[1].Id,
                        new List<string>
                        {
                            path2
                        }
                    }
                },
                new Dictionary<string, List<string>>
                {
                    {
                        subscriptions[1].Id,
                        new List<string>
                        {
                            path3
                        }
                    }
                }
            };

            using (TestContext context = new TestContext(subscriptions, pathData))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            path1
                        }
                    },
                    {
                        subscriptions[1],
                        new List<string>
                        {
                            path2,
                            path3
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies that no build will be queued if no paths are specified.
        /// </summary>
        [Fact]
        public async Task QueueBuildCommand_NoBaseImageChange()
        {
            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription("repo1")
            };

            List<Dictionary<string, List<string>>> pathData = new List<Dictionary<string, List<string>>>
            {
                new Dictionary<string, List<string>>
                {
                    {
                        subscriptions[0].Id,
                        new List<string>()
                    }
                },
                new Dictionary<string, List<string>>
                {
                    {
                        subscriptions[0].Id,
                        new List<string>()
                    }
                }
            };

            using (TestContext context = new TestContext(subscriptions, pathData))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            // No paths are expected
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued build a subscription is spread
        /// across multiple path sets.
        /// </summary>
        [Fact]
        public async Task QueueBuildCommand_Subscription_MultiSet()
        {
            const string path1 = "path1";
            const string path2 = "path2";
            const string path3 = "path3";

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription("repo1")
            };

            List<Dictionary<string, List<string>>> pathData = new List<Dictionary<string, List<string>>>
            {
                new Dictionary<string, List<string>>
                {
                    {
                        subscriptions[0].Id,
                        new List<string>
                        {
                            path1,
                            path2
                        }
                    }
                },
                new Dictionary<string, List<string>>
                {
                    {
                        subscriptions[0].Id,
                        new List<string>
                        {
                            path3
                        }
                    }
                }
            };

            using (TestContext context = new TestContext(subscriptions, pathData))
            {
                await context.ExecuteCommandAsync();

                Dictionary<Subscription, IList<string>> expectedPathsBySubscription =
                    new Dictionary<Subscription, IList<string>>
                {
                    {
                        subscriptions[0],
                        new List<string>
                        {
                            path1,
                            path2,
                            path3
                        }
                    }
                };

                context.Verify(expectedPathsBySubscription);
            }
        }

        /// <summary>
        /// Use this method to generate a unique repo owner name for the tests. This ensures that each test
        /// uses a different name and prevents collisions when running the tests in parallel. This is because
        /// the <see cref="QueueBuildCommand"/> generates temp folders partially based on the name of
        /// the repo owner.
        /// </summary>
        private static string GetRepoOwner([CallerMemberName] string testMethodName = null, string suffix = null)
        {
            return testMethodName + suffix;
        }

        private static Subscription CreateSubscription(
            string repoName,
            int index = 0,
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
                }
            };
        }

        /// <summary>
        /// Sets up the test state from the provided metadata, executes the test, and verifies the results.
        /// </summary>
        private class TestContext : IDisposable
        {
            private readonly bool hasInProgressBuild;
            private readonly List<string> filesToCleanup = new List<string>();
            private readonly List<string> foldersToCleanup = new List<string>();
            private readonly string subscriptionsPath;
            private readonly Mock<IBuildHttpClient> buildHttpClientMock;
            private readonly QueueBuildCommand command;
            private readonly IEnumerable<Dictionary<string, List<string>>> pathData;

            private const string BuildOrganization = "testOrg";

            /// <summary>
            /// Initializes a new instance of <see cref="TestContext"/>.
            /// </summary>
            /// <param name="subscriptions">The set of subscription metadata describing the Git repos that are listening for changes to base images.</param>
            /// <param name="hasInProgressBuild">A value indicating whether to mark a build to be in progress for all pipelines.</param>
            public TestContext(
                Subscription[] subscriptions,
                IEnumerable<Dictionary<string, List<string>>> pathData,
                bool hasInProgressBuild = false)
            {
                this.pathData = pathData;
                this.hasInProgressBuild = hasInProgressBuild;

                this.subscriptionsPath = this.SerializeJsonObjectToTempFile(subscriptions);

                TeamProject project = new TeamProject
                {
                    Id = Guid.NewGuid()
                };

                Mock<IProjectHttpClient> projectHttpClientMock = CreateProjectHttpClientMock(project);
                this.buildHttpClientMock = CreateBuildHttpClientMock(project, hasInProgressBuild);
                Mock<IVssConnectionFactory> connectionFactoryMock = CreateVssConnectionFactoryMock(
                    projectHttpClientMock, this.buildHttpClientMock);

                this.command = this.CreateCommand(connectionFactoryMock);
            }

            public Task ExecuteCommandAsync()
            {
                return this.command.ExecuteAsync();
            }

            /// <summary>
            /// Verifies the test execution to ensure the results match the expected state.
            /// </summary>
            /// <param name="expectedPathsBySubscription">
            /// A mapping of subscription metadata to the list of expected path args passed to the queued build, if any.
            /// </param>
            public void Verify(IDictionary<Subscription, IList<string>> expectedPathsBySubscription = null)
            {
                if (this.hasInProgressBuild)
                {
                    // If a build was marked as in progress for the pipelines, then no build is expected to ever be queued.
                    this.buildHttpClientMock.Verify(o => o.QueueBuildAsync(It.IsAny<Build>()), Times.Never);
                }
                else
                {
                    if (expectedPathsBySubscription == null)
                    {
                        throw new ArgumentNullException(nameof(expectedPathsBySubscription));
                    }

                    foreach (KeyValuePair<Subscription, IList<string>> kvp in expectedPathsBySubscription)
                    {
                        if (kvp.Value.Any())
                        {
                            this.buildHttpClientMock
                                .Verify(o =>
                                    o.QueueBuildAsync(
                                        It.Is<Build>(build => FilterBuildToSubscription(build, kvp.Key, kvp.Value))));
                        }
                        else
                        {
                            this.buildHttpClientMock.Verify(o => o.QueueBuildAsync(It.IsAny<Build>()), Times.Never);
                        }
                    }
                }
            }

            private string SerializeJsonObjectToTempFile(object jsonObject)
            {
                string path = Path.GetTempFileName();
                File.WriteAllText(path, JsonConvert.SerializeObject(jsonObject));
                this.filesToCleanup.Add(path);
                return path;
            }

            private QueueBuildCommand CreateCommand(Mock<IVssConnectionFactory> connectionFactoryMock)
            {
                Mock<ILoggerService> loggerServiceMock = new Mock<ILoggerService>();

                QueueBuildCommand command = new QueueBuildCommand(connectionFactoryMock.Object, loggerServiceMock.Object);
                command.Options.BuildOrganization = BuildOrganization;
                command.Options.BuildPersonalAccessToken = "testToken";
                command.Options.SubscriptionsPath = this.subscriptionsPath;
                IEnumerable<string> subscriptions = this.pathData
                    .Select(data => JsonConvert.SerializeObject(data.ToDictionary(kvp => kvp.Key, kvp => String.Join(" ", kvp.Value.ToArray()))));
                command.Options.Subscriptions = subscriptions;

                return command;
            }

            private static Mock<IVssConnectionFactory> CreateVssConnectionFactoryMock(
                Mock<IProjectHttpClient> projectHttpClientMock,
                Mock<IBuildHttpClient> buildHttpClientMock)
            {
                Mock<IVssConnection> connectionMock = CreateVssConnectionMock(projectHttpClientMock, buildHttpClientMock);

                Mock<IVssConnectionFactory> connectionFactoryMock = new Mock<IVssConnectionFactory>();
                connectionFactoryMock
                    .Setup(o => o.Create(
                        It.Is<Uri>(uri => uri.ToString() == $"https://dev.azure.com/{BuildOrganization}"),
                        It.IsAny<VssCredentials>()))
                    .Returns(connectionMock.Object);
                return connectionFactoryMock;
            }

            private static Mock<IVssConnection> CreateVssConnectionMock(Mock<IProjectHttpClient> projectHttpClientMock,
                Mock<IBuildHttpClient> buildHttpClientMock)
            {
                Mock<IVssConnection> connectionMock = new Mock<IVssConnection>();
                connectionMock
                    .Setup(o => o.GetProjectHttpClient())
                    .Returns(projectHttpClientMock.Object);
                connectionMock
                    .Setup(o => o.GetBuildHttpClient())
                    .Returns(buildHttpClientMock.Object);
                return connectionMock;
            }

            private static Mock<IProjectHttpClient> CreateProjectHttpClientMock(TeamProject project)
            {
                Mock<IProjectHttpClient> projectHttpClientMock = new Mock<IProjectHttpClient>();
                projectHttpClientMock
                    .Setup(o => o.GetProjectAsync(It.IsAny<string>()))
                    .ReturnsAsync(project);
                return projectHttpClientMock;
            }

            private static Mock<IBuildHttpClient> CreateBuildHttpClientMock(TeamProject project, bool hasInProgressBuild)
            {
                PagedList<Build> builds = new PagedList<Build>();
                if (hasInProgressBuild)
                {
                    builds.Add(new Build());
                }

                Mock<IBuildHttpClient> buildHttpClientMock = new Mock<IBuildHttpClient>();
                buildHttpClientMock
                    .Setup(o => o.GetBuildsAsync(project.Id, It.IsAny<IEnumerable<int>>(), It.IsAny<BuildStatus>()))
                    .ReturnsAsync((IPagedList<Build>)builds);

                return buildHttpClientMock;
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
                    .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim().Trim('\''))
                    .ToList();
                return CompareLists(expectedPaths, paths);
            }

            /// <summary>
            /// Returns a value indicating whether the two lists are equivalent (order does not matter).
            /// </summary>
            private static bool CompareLists(IList<string> expectedPaths, IList<string> paths)
            {
                if (paths.Count != expectedPaths.Count)
                {
                    return false;
                }

                paths = paths
                    .OrderBy(p => p)
                    .ToList();
                expectedPaths = expectedPaths
                    .OrderBy(p => p)
                    .ToList();

                for (int i = 0; i < paths.Count; i++)
                {
                    if (paths[i] != expectedPaths[i])
                    {
                        return false;
                    }
                }

                return true;
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

        private class PagedList<T> : List<T>, IPagedList<T>
        {
            public string ContinuationToken => throw new NotImplementedException();
        }
    }
}
