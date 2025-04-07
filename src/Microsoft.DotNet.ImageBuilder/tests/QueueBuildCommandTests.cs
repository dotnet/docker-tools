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
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using IVssConnection = Microsoft.DotNet.ImageBuilder.Services.IVssConnection;
using WebApi = Microsoft.TeamFoundation.Build.WebApi;

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

            List<List<SubscriptionImagePaths>> allSubscriptionImagePaths = new()
            {
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[0].Id,
                        ImagePaths = new string[]
                        {
                            path1
                        }
                    }
                }
            };

            PagedList<WebApi.Build> inProgressBuilds = new()
            {
                CreateBuild("http://contoso")
            };

            using (TestContext context = new(subscriptions, allSubscriptionImagePaths, inProgressBuilds, new PagedList<WebApi.Build>()))
            {
                await context.ExecuteCommandAsync();

                // Normally this state would cause a build to be queued but since
                // a build is marked as in progress, it doesn't.

                context.Verify(notificationPostCallCount: 1, isQueuedBuildExpected: false);
            }
        }

        /// <summary>
        /// Verifies that no build is queued if there are too many recent failed builds.
        /// </summary>
        [Fact]
        public async Task QueueBuildCommand_RecentFailedBuilds_MaxFailed()
        {
            const string path1 = "path1";

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription("repo1")
            };

            List<List<SubscriptionImagePaths>> allSubscriptionImagePaths = new()
            {
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[0].Id,
                        ImagePaths = new string[]
                        {
                            path1
                        }
                    }
                }
            };

            PagedList<WebApi.Build> allBuilds = new();

            for (int i = 0; i < QueueBuildCommand.BuildFailureLimit; i++)
            {
                WebApi.Build failedBuild = CreateBuild($"https://failedbuild-{i}");
                failedBuild.Tags.Add(AzdoTags.AutoBuilder);
                failedBuild.Status = WebApi.BuildStatus.Completed;
                failedBuild.Result = WebApi.BuildResult.Failed;
                allBuilds.Add(failedBuild);
            }

            using TestContext context = new(subscriptions, allSubscriptionImagePaths, new PagedList<WebApi.Build>(), allBuilds);
            await context.ExecuteCommandAsync();

            context.Verify(notificationPostCallCount: 1, isQueuedBuildExpected: false);
        }

        /// <summary>
        /// Verifies that a build is queued even if there are recent failed builds but not enough to meet the threshold.
        /// </summary>
        [Fact]
        public async Task QueueBuildCommand_RecentFailedBuilds_PartialFailed()
        {
            const string path1 = "path1";

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription("repo1")
            };

            List<List<SubscriptionImagePaths>> allSubscriptionImagePaths = new()
            {
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[0].Id,
                        ImagePaths = new string[]
                        {
                            path1
                        }
                    }
                }
            };

            PagedList<WebApi.Build> allBuilds = new();

            for (int i = 0; i < QueueBuildCommand.BuildFailureLimit - 1; i++)
            {
                WebApi.Build failedBuild = CreateBuild($"https://failedbuild-{i}");
                failedBuild.Tags.Add(AzdoTags.AutoBuilder);
                failedBuild.Status = WebApi.BuildStatus.Completed;
                failedBuild.Result = WebApi.BuildResult.Failed;
                allBuilds.Add(failedBuild);
            }

            using TestContext context = new(subscriptions, allSubscriptionImagePaths, new PagedList<WebApi.Build>(), allBuilds);
            await context.ExecuteCommandAsync();


            Dictionary<Subscription, IList<string>> expectedPathsBySubscription = new()
            {
                {
                    subscriptions[0],
                    new List<string>
                    {
                        path1
                    }
                }
            };

            context.Verify(notificationPostCallCount: 1, isQueuedBuildExpected: true, expectedPathsBySubscription);
        }

        /// <summary>
        /// Verifies that a build is queued when the set of recent builds consist of some recently succeeded builds.
        /// </summary>
        [Fact]
        public async Task QueueBuildCommand_RecentFailedBuilds_SucceededAndFailed()
        {
            const string path1 = "path1";

            Subscription[] subscriptions = new Subscription[]
            {
                CreateSubscription("repo1")
            };

            List<List<SubscriptionImagePaths>> allSubscriptionImagePaths = new()
            {
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[0].Id,
                        ImagePaths = new string[]
                        {
                            path1
                        }
                    }
                }
            };

            PagedList<WebApi.Build> allBuilds = new();

            WebApi.Build succeeded = CreateBuild($"https://succeededbuild");
            succeeded.Tags.Add(AzdoTags.AutoBuilder);
            succeeded.Status = WebApi.BuildStatus.Completed;
            succeeded.Result = WebApi.BuildResult.Succeeded;
            allBuilds.Add(succeeded);

            for (int i = 0; i < QueueBuildCommand.BuildFailureLimit; i++)
            {
                WebApi.Build failedBuild = CreateBuild($"https://failedbuild-{i}");
                failedBuild.Tags.Add(AzdoTags.AutoBuilder);
                failedBuild.Status = WebApi.BuildStatus.Completed;
                failedBuild.Result = WebApi.BuildResult.Failed;
                allBuilds.Add(failedBuild);
            }

            using TestContext context = new(subscriptions, allSubscriptionImagePaths, new PagedList<WebApi.Build>(), allBuilds);
            await context.ExecuteCommandAsync();

            Dictionary<Subscription, IList<string>> expectedPathsBySubscription = new()
            {
                {
                    subscriptions[0],
                    new List<string>
                    {
                        path1
                    }
                }
            };

            context.Verify(notificationPostCallCount: 1, isQueuedBuildExpected: true, expectedPathsBySubscription);
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

            List<List<SubscriptionImagePaths>> allSubscriptionImagePaths = new List<List<SubscriptionImagePaths>>
            {
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[0].Id,
                        ImagePaths = new string[]
                        {
                            path1
                        }
                    },
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[1].Id,
                        ImagePaths = new string[]
                        {
                            path2
                        }
                    }
                },
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[1].Id,
                        ImagePaths = new string[]
                        {
                            path3
                        }
                    }
                }
            };

            using (TestContext context = new TestContext(subscriptions, allSubscriptionImagePaths))
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

                context.Verify(notificationPostCallCount: 2, isQueuedBuildExpected: true, expectedPathsBySubscription);
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

            List<List<SubscriptionImagePaths>> allSubscriptionImagePaths = new List<List<SubscriptionImagePaths>>
            {
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[0].Id,
                        ImagePaths = Array.Empty<string>()
                    }
                },
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[0].Id,
                        ImagePaths = Array.Empty<string>()
                    }
                }
            };

            using (TestContext context = new TestContext(subscriptions, allSubscriptionImagePaths))
            {
                await context.ExecuteCommandAsync();

                context.Verify(notificationPostCallCount: 0, isQueuedBuildExpected: false);
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

            List<List<SubscriptionImagePaths>> allSubscriptionImagePaths = new List<List<SubscriptionImagePaths>>
            {
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[0].Id,
                        ImagePaths = new string[]
                        {
                            path1,
                            path2
                        }
                    }
                },
                new List<SubscriptionImagePaths>
                {
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptions[0].Id,
                        ImagePaths = new string[]
                        {
                            path3
                        }
                    }
                }
            };

            using (TestContext context = new TestContext(subscriptions, allSubscriptionImagePaths))
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

                context.Verify(notificationPostCallCount: 1, isQueuedBuildExpected: true, expectedPathsBySubscription);
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
                    Owner = "dotnetOwner",
                    Repo = "versionsRepo",
                    Branch = "mainBranch",
                    Path = "docker/image-info.json"
                }
            };
        }

        private static WebApi.Build CreateBuild(string url)
        {
            WebApi.Build build = new() { Uri = new Uri(url) };
            build.Links.AddLink("web", $"{url}/web");
            return build;
        }

        /// <summary>
        /// Sets up the test state from the provided metadata, executes the test, and verifies the results.
        /// </summary>
        private class TestContext : IDisposable
        {
            private readonly PagedList<WebApi.Build> inProgressBuilds;
            private readonly PagedList<WebApi.Build> allBuilds;
            private readonly List<string> filesToCleanup = new List<string>();
            private readonly List<string> foldersToCleanup = new List<string>();
            private readonly string subscriptionsPath;
            private readonly Mock<IBuildHttpClient> buildHttpClientMock;
            private readonly QueueBuildCommand command;
            private readonly IEnumerable<IEnumerable<SubscriptionImagePaths>> allSubscriptionImagePaths;
            private readonly Mock<INotificationService> _notificationServiceMock;

            private const string BuildOrganization = "testOrg";
            private const string GitOwner = "git-owner";
            private const string GitRepo = "git-repo";
            private const string GitAccessToken = "git-pat";

            private static readonly GitHubAuthOptions s_gitHubAuthOptions = new(AuthToken: GitAccessToken);

            /// <summary>
            /// Initializes a new instance of <see cref="TestContext"/>.
            /// </summary>
            /// <param name="subscriptions">The set of subscription metadata describing the Git repos that are listening for changes to base images.</param>
            /// <param name="allSubscriptionImagePaths">Multiple sets of mappings between subscriptions and their associated image paths.</param>
            public TestContext(
                Subscription[] subscriptions,
                IEnumerable<IEnumerable<SubscriptionImagePaths>> allSubscriptionImagePaths)
                : this(subscriptions, allSubscriptionImagePaths, new PagedList<WebApi.Build>(), new PagedList<WebApi.Build>())
            {
            }

            /// <summary>
            /// Initializes a new instance of <see cref="TestContext"/>.
            /// </summary>
            /// <param name="subscriptions">The set of subscription metadata describing the Git repos that are listening for changes to base images.</param>
            /// <param name="allSubscriptionImagePaths">Multiple sets of mappings between subscriptions and their associated image paths.</param>
            /// <param name="inProgressBuilds">The set of in-progress builds that should be configured.</param>
            /// <param name="allBuilds">The set of failed builds that should be configured.</param>
            public TestContext(
                Subscription[] subscriptions,
                IEnumerable<IEnumerable<SubscriptionImagePaths>> allSubscriptionImagePaths,
                PagedList<WebApi.Build> inProgressBuilds,
                PagedList<WebApi.Build> allBuilds)
            {
                this.allSubscriptionImagePaths = allSubscriptionImagePaths;
                this.inProgressBuilds = inProgressBuilds;
                this.allBuilds = allBuilds;

                this.subscriptionsPath = this.SerializeJsonObjectToTempFile(subscriptions);

                TeamProject project = new TeamProject
                {
                    Id = Guid.NewGuid()
                };

                Mock<IProjectHttpClient> projectHttpClientMock = CreateProjectHttpClientMock(project);
                this.buildHttpClientMock = CreateBuildHttpClientMock(project, this.inProgressBuilds, this.allBuilds);
                Mock<IVssConnectionFactory> connectionFactoryMock = CreateVssConnectionFactoryMock(
                    projectHttpClientMock, this.buildHttpClientMock);

                _notificationServiceMock = new Mock<INotificationService>();

                this.command = this.CreateCommand(connectionFactoryMock);
            }

            public Task ExecuteCommandAsync()
            {
                return this.command.ExecuteAsync();
            }

            /// <summary>
            /// Verifies the test execution to ensure the results match the expected state.
            /// </summary>
            /// <param name="notificationPostCallCount">
            /// Number of times a post to notify the GitHub repo is expected.
            /// </param>
            /// <param name="isQueuedBuildExpected">
            /// Indicates whether a build was expected to be queued.
            /// </param>
            /// <param name="expectedPathsBySubscription">
            /// A mapping of subscription metadata to the list of expected path args passed to the queued build, if any.
            /// </param>
            public void Verify(int notificationPostCallCount, bool isQueuedBuildExpected, IDictionary<Subscription, IList<string>> expectedPathsBySubscription = null)
            {
                _notificationServiceMock
                    .Verify(o => o.PostAsync(
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<IEnumerable<string>>(),
                            GitOwner,
                            GitRepo,
                            s_gitHubAuthOptions,
                            It.IsAny<bool>(),
                            It.IsAny<IEnumerable<string>>()),
                        Times.Exactly(notificationPostCallCount));

                if (!isQueuedBuildExpected)
                {
                    this.buildHttpClientMock.Verify(o => o.QueueBuildAsync(It.IsAny<WebApi.Build>()), Times.Never);
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
                                        It.Is<WebApi.Build>(build => FilterBuildToSubscription(build, kvp.Key, kvp.Value))));
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
                Mock<ILoggerService> loggerServiceMock = new();

                QueueBuildCommand command = new(connectionFactoryMock.Object, loggerServiceMock.Object, _notificationServiceMock.Object);
                command.Options.AzdoOptions.Organization = BuildOrganization;
                command.Options.AzdoOptions.AccessToken = "testToken";
                command.Options.SubscriptionsPath = this.subscriptionsPath;
                command.Options.AllSubscriptionImagePaths = this.allSubscriptionImagePaths
                    .Select(subscriptionImagePaths => JsonConvert.SerializeObject(subscriptionImagePaths.ToArray()));

                command.Options.GitOptions.Owner = GitOwner;
                command.Options.GitOptions.Repo = GitRepo;
                command.Options.GitOptions.GitHubAuthOptions = s_gitHubAuthOptions;

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

            private static Mock<IBuildHttpClient> CreateBuildHttpClientMock(TeamProject project,
                PagedList<WebApi.Build> inProgressBuilds, PagedList<WebApi.Build> failedBuilds)
            {
                WebApi.Build build = CreateBuild("https://contoso");

                Mock<IBuildHttpClient> buildHttpClientMock = new();
                buildHttpClientMock
                    .Setup(o => o.GetBuildsAsync(project.Id, It.IsAny<IEnumerable<int>>(), WebApi.BuildStatus.InProgress))
                    .ReturnsAsync(inProgressBuilds);

                buildHttpClientMock
                    .Setup(o => o.GetBuildsAsync(project.Id, It.IsAny<IEnumerable<int>>(), null))
                    .ReturnsAsync(failedBuilds);

                buildHttpClientMock
                    .Setup(o => o.QueueBuildAsync(It.IsAny<WebApi.Build>()))
                    .ReturnsAsync(build);

                return buildHttpClientMock;
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
                    .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim().Trim('\''))
                    .Except([ CliHelper.FormatAlias(DockerfileFilterOptionsBuilder.PathOptionName) ])
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
            public string ContinuationToken
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }
        }
    }
}
