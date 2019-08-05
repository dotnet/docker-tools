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
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
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
    public class RebuildStaleImagesCommandTests
    {
        /// <summary>
        /// Verifies the correct path arguments are passed to the queued build for a basic
        /// scenario involving one image that has changed.
        /// </summary>
        [Fact]
        public async Task RebuildStaleImagesCommand_SingleDigestChanged()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1";
            const string dockerfile2Path = "dockerfile2";

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
                                ManifestHelper.CreatePlatform(dockerfile1Path, "tag1"),
                                ManifestHelper.CreatePlatform(dockerfile2Path, "tag2"))))
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
        /// Verifies the correct path arguments are passed to the queued build when
        /// the images have no data reflected in the image info data.
        /// </summary>
        [Fact]
        public async Task RebuildStaleImagesCommand_MissingImageInfo()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1";
            const string dockerfile2Path = "dockerfile2";

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
                                ManifestHelper.CreatePlatform(dockerfile1Path, "tag1"),
                                ManifestHelper.CreatePlatform(dockerfile2Path, "tag2"))))
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
        /// Verifies that no build is queued if a build is currently in progress.
        /// </summary>
        [Fact]
        public async Task RebuildStaleImagesCommand_BuildInProgress()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1";

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
                                ManifestHelper.CreatePlatform(dockerfile1Path, "tag1"))))
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
                }
            };

            using (TestContext context =
                new TestContext(imageInfoData, subscriptions, subscriptionManifests, dockerfileInfos, hasInProgressBuild: true))
            {
                await context.ExecuteCommandAsync();

                // Normally this state would cause a build to be queued but since
                // a build is marked as in progress, it doesn't.

                context.Verify();
            }
        }

        /// <summary>
        /// Verifies the correct path arguments are passed to the queued builds for two
        /// subscriptions that have changed images.
        /// </summary>
        [Fact]
        public async Task RebuildStaleImagesCommand_MultiSubscription()
        {
            const string repo1 = "test-repo";
            const string repo2 = "test-repo2";
            const string dockerfile1Path = "dockerfile1";
            const string dockerfile2Path = "dockerfile2";

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
                                ManifestHelper.CreatePlatform(dockerfile1Path, "tag1"))))
                },
                {
                    subscriptions[1],
                    ManifestHelper.CreateManifest(
                        ManifestHelper.CreateRepo(
                            repo2,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(dockerfile2Path, "tag2"))))
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
        /// Verifies that a base image's digest will be cached and not pulled for a subsequent image.
        /// </summary>
        [Fact]
        public async Task RebuildStaleImagesCommand_BaseImageCaching()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1";
            const string dockerfile2Path = "dockerfile2";
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
                                ManifestHelper.CreatePlatform(dockerfile1Path, "tag1"),
                                ManifestHelper.CreatePlatform(dockerfile2Path, "tag2"))))
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
        public async Task RebuildStaleImagesCommand_NoBaseImageChange()
        {
            const string repo1 = "test-repo";
            const string dockerfile1Path = "dockerfile1";
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
                                ManifestHelper.CreatePlatform(dockerfile1Path, "tag1"))))
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
        /// Verifies the correct path arguments are passed to the queued build when
        /// a base image changes where the image referencing that base image has other
        /// images dependent upon it.
        /// </summary>
        [Fact]
        public async Task RebuildStaleImagesCommand_DependencyGraph()
        {
            const string runtimeDepsRepo = "runtimedeps-repo";
            const string runtimeRepo = "runtime-repo";
            const string sdkRepo = "sdk-repo";
            const string aspnetRepo = "aspnet-repo";
            const string otherRepo = "other-repo";
            const string runtimeDepsDockerfilePath = "runtime-deps/dockerfile1";
            const string runtimeDockerfilePath = "runtime/dockerfile2";
            const string sdkDockerfilePath = "sdk/dockerfile3";
            const string aspnetDockerfilePath = "aspnet/dockerfile4";
            const string otherDockerfilePath = "other/dockerfile";
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
                                ManifestHelper.CreatePlatform(runtimeDepsDockerfilePath, "tag1"))),
                        ManifestHelper.CreateRepo(
                            runtimeRepo,
                            ManifestHelper.CreateImage(
                                CreatePlatformWithRepoBuildArg(runtimeDockerfilePath, "runtime-deps", "tag1"))),
                        ManifestHelper.CreateRepo(
                            sdkRepo,
                            ManifestHelper.CreateImage(
                                CreatePlatformWithRepoBuildArg(sdkDockerfilePath, "runtime", "tag1"))),
                        ManifestHelper.CreateRepo(
                            aspnetRepo,
                            ManifestHelper.CreateImage(
                                CreatePlatformWithRepoBuildArg(aspnetDockerfilePath, "runtime", "tag1"))),
                        ManifestHelper.CreateRepo(
                            otherRepo,
                            ManifestHelper.CreateImage(
                                ManifestHelper.CreatePlatform(otherDockerfilePath, "tag1"))))
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
        /// Use this method to generate a unique repo owner name for the tests. This ensures that each test
        /// uses a different name and prevents collisions when running the tests in parallel. This is because
        /// the <see cref="RebuildStaleImagesCommand"/> generates temp folders partially based on the name of
        /// the repo owner.
        /// </summary>
        private static string GetRepoOwner([CallerMemberName] string testMethodName = null, string suffix = null)
        {
            return testMethodName + suffix;
        }

        private static Platform CreatePlatformWithRepoBuildArg(string dockerfilePath, string repo, params string[] tags)
        {
            Platform platform = ManifestHelper.CreatePlatform(dockerfilePath, tags);
            platform.BuildArgs = new Dictionary<string, string>
            {
                { "REPO", repo }
            };
            return platform;
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
            private readonly Dictionary<string, string> imageDigests = new Dictionary<string, string>();
            private readonly string subscriptionsPath;
            private readonly string imageInfoPath;
            private readonly IHttpClientFactory httpClientFactory;
            private readonly Mock<IBuildHttpClient> buildHttpClientMock;
            private readonly RebuildStaleImagesCommand command;

            private const string BuildOrganization = "testOrg";

            public Mock<IDockerService> DockerServiceMock { get; }

            /// <summary>
            /// Initializes a new instance of <see cref="TestContext"/>.
            /// </summary>
            /// <param name="imageInfoData">The set of image info data for all Git repos.</param>
            /// <param name="subscriptions">The set of subscription metadata describing the Git repos that are listening for changes to base images.</param>
            /// <param name="subscriptionManifests">A mapping of subscriptions to their associated manifests.</param>
            /// <param name="dockerfileInfos">A mapping of Git repos to their associated set of Dockerfiles.</param>
            /// <param name="hasInProgressBuild">A value indicating whether to mark a build to be in progress for all pipelines.</param>
            public TestContext(
                RepoData[] imageInfoData,
                Subscription[] subscriptions,
                IDictionary<Subscription, Manifest> subscriptionManifests,
                Dictionary<GitRepo, List<DockerfileInfo>> dockerfileInfos,
                bool hasInProgressBuild = false)
            {
                this.hasInProgressBuild = hasInProgressBuild;

                this.imageInfoPath = this.SerializeJsonObjectToTempFile(imageInfoData);
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

                Mock<IProjectHttpClient> projectHttpClientMock = CreateProjectHttpClientMock(project);
                this.buildHttpClientMock = CreateBuildHttpClientMock(project, hasInProgressBuild);
                Mock<IVssConnectionFactory> connectionFactoryMock = CreateVssConnectionFactoryMock(
                    projectHttpClientMock, this.buildHttpClientMock);

                this.httpClientFactory = CreateHttpClientFactory(subscriptions, subscriptionManifests, dockerfileInfos);

                this.DockerServiceMock = this.CreateDockerServiceMock();
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

            private RebuildStaleImagesCommand CreateCommand(Mock<IVssConnectionFactory> connectionFactoryMock)
            {
                Mock<ILoggerService> loggerServiceMock = new Mock<ILoggerService>();

                RebuildStaleImagesCommand command = new RebuildStaleImagesCommand(
                    this.DockerServiceMock.Object, connectionFactoryMock.Object, this.httpClientFactory,
                    loggerServiceMock.Object);
                command.Options.BuildOrganization = BuildOrganization;
                command.Options.BuildPersonalAccessToken = "testToken";
                command.Options.SubscriptionsPath = this.subscriptionsPath;
                command.Options.ImageInfoPath = this.imageInfoPath;
                return command;
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
                    Manifest manifest = subscriptionManifests[subscription];
                    List<DockerfileInfo> repoDockerfileInfos = dockerfileInfos[subscription.RepoInfo];
                    string repoZipPath = GenerateRepoZipFile(subscription, manifest, repoDockerfileInfos);

                    responses.Add(
                        $"https://www.github.com/{subscription.RepoInfo.Owner}/{subscription.RepoInfo.Name}/archive/{subscription.RepoInfo.Branch}.zip",
                        new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new ByteArrayContent(File.ReadAllBytes(repoZipPath))
                        });
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
                    Path.Combine(destinationPath, dockerfileInfo.DockerfilePath)).FullName;
                File.WriteAllText(Path.Combine(dockerfilePath, "Dockerfile"), dockerfileContents);
            }

            private Mock<IDockerService> CreateDockerServiceMock()
            {
                Mock<IDockerService> dockerServiceMock = new Mock<IDockerService>();
                dockerServiceMock
                    .Setup(o => o.GetImageDigest(It.IsAny<string>(), false))
                    .Returns((string image, bool isDryRun) => this.imageDigests[image]);
                return dockerServiceMock;
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
                    .Split("--path ", StringSplitOptions.RemoveEmptyEntries)
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
