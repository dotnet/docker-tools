#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Threading;
using Azure;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ContainerRegistryHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class CleanAcrImagesCommandTest
    {
        private const string AcrName = "myacr.azurecr.io";

        [TestMethod]
        public async Task StagingRepos()
        {
            const string stagingRepo1Name = "build-staging/repo1";
            const string stagingRepo2Name = "build-staging/repo2";
            const string repo1Digest1 = "sha256:repo1digest1";
            const string repo2Digest1 = "sha256:repo2digest1";

            ContainerRepository nonPublicRepo1 = CreateContainerRepository(
                stagingRepo1Name,
                CreateContainerRepositoryProperties(lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(14))),
                [
                    CreateArtifactManifestProperties(repositoryName: stagingRepo1Name, digest: repo1Digest1)
                ]);

            ContainerRepository nonPublicRepo2 = CreateContainerRepository(
                stagingRepo2Name,
                CreateContainerRepositoryProperties(lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(16))),
                [
                    CreateArtifactManifestProperties(repositoryName: stagingRepo2Name, digest: repo2Digest1)
                ]);

            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([nonPublicRepo1, nonPublicRepo2]);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(stagingRepo2Name))
                .Returns(Task.CompletedTask);

            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);

            CleanAcrImagesCommand command = new(
                acrClientFactory, Mock.Of<IAcrContentClientFactory>(), Mock.Of<ILogger<CleanAcrImagesCommand>>(), Mock.Of<ILifecycleMetadataService>(), Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "build-staging/*";
            command.Options.Action = CleanAcrImagesAction.Delete;
            command.Options.Age = 15;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(stagingRepo1Name), Times.Never);
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(stagingRepo2Name));
        }

        [TestMethod]
        public async Task PublicNightlyRepos()
        {
            const string publicRepo1Name = "public/dotnet/core-nightly/repo1";
            const string publicRepo2Name = "public/dotnet/core/repo2";
            const string publicRepo3Name = "public/dotnet/core-nightly/repo3";
            const string publicRepo4Name = "public/dotnet/nightly/repo4";

            const string repo1Digest1 = "sha256:repo1digest1";
            const string repo1Digest2 = "sha256:repo1digest2";
            const string repo3Digest1 = "sha256:repo3digest1";
            const string repo3Digest2 = "sha256:repo3digest2";
            const string repo4Digest1 = "sha256:repo4digest1";

            ContainerRepository publicRepo1 = CreateContainerRepository(
                publicRepo1Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: publicRepo1Name, digest: repo1Digest1, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(1))),
                    CreateArtifactManifestProperties(repositoryName: publicRepo1Name, digest: repo1Digest2, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31)), tags: ["tag"])
                ]);

            ContainerRepository publicRepo2 = CreateContainerRepository(publicRepo2Name, new ContainerRepositoryProperties(), []);

            ContainerRepository publicRepo3 = CreateContainerRepository(
                publicRepo3Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: publicRepo3Name, digest: repo3Digest1, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(29))),
                    CreateArtifactManifestProperties(repositoryName: publicRepo3Name, digest: repo3Digest2, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31)))
                ]);

            ContainerRepository publicRepo4 = CreateContainerRepository(
                publicRepo4Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: publicRepo4Name, digest: repo4Digest1, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(60)))
                ]);

            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([publicRepo1, publicRepo2, publicRepo3, publicRepo4]);
            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);

            Mock<IAcrContentClient> repo1ContentClient = CreateAcrContentClientMock(publicRepo1Name);
            Mock<IAcrContentClient> repo2ContentClient = CreateAcrContentClientMock(publicRepo2Name);
            Mock<IAcrContentClient> repo3ContentClient = CreateAcrContentClientMock(publicRepo3Name);
            Mock<IAcrContentClient> repo4ContentClient = CreateAcrContentClientMock(publicRepo4Name);

            IAcrContentClientFactory acrContentClientFactory = CreateAcrContentClientFactory(
                AcrName, [repo1ContentClient, repo2ContentClient, repo3ContentClient, repo4ContentClient]);

            CleanAcrImagesCommand command = new(
                acrClientFactory, acrContentClientFactory, Mock.Of<ILogger<CleanAcrImagesCommand>>(), Mock.Of<ILifecycleMetadataService>(), Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "public/dotnet/*nightly/*";
            command.Options.Action = CleanAcrImagesAction.PruneDangling;
            command.Options.Age = 30;

            await command.ExecuteAsync();

            repo1ContentClient.Verify(o => o.DeleteManifestAsync(repo1Digest1), Times.Never);
            repo1ContentClient.Verify(o => o.DeleteManifestAsync(repo1Digest2), Times.Never);
            repo2ContentClient.Verify(o => o.DeleteManifestAsync(It.IsAny<string>()), Times.Never);
            repo3ContentClient.Verify(o => o.DeleteManifestAsync(repo3Digest1), Times.Never);
            repo3ContentClient.Verify(o => o.DeleteManifestAsync(repo3Digest2));
            repo4ContentClient.Verify(o => o.DeleteManifestAsync(repo4Digest1));
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(publicRepo4Name), Times.Never);
        }

        /// <summary>
        /// Validates that an empty test repo will be deleted.
        /// </summary>
        [TestMethod]
        public async Task DeleteEmptyTestRepo()
        {
            const string repo1Name = "test/repo1";
            const string repo2Name = "test/repo2";

            const string repo1Digest1 = "sha256:repo1digest1";

            ContainerRepository repo1 = CreateContainerRepository(
                repo1Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: repo1Name, digest: repo1Digest1, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(1)))
                ]);

            ContainerRepository repo2 = CreateContainerRepository(repo2Name, CreateContainerRepositoryProperties(), []);

            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([repo1, repo2]);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(repo2Name))
                .Returns(Task.CompletedTask);

            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);

            CleanAcrImagesCommand command = new(
                acrClientFactory, Mock.Of<IAcrContentClientFactory>(), Mock.Of<ILogger<CleanAcrImagesCommand>>(), Mock.Of<ILifecycleMetadataService>(), Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo1Name), Times.Never);
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo2Name));
        }

        /// <summary>
        /// Validates that every expired image in a repository is deleted.
        /// </summary>
        [TestMethod]
        public async Task DeleteAllExpiredImages()
        {
            const string repo1Name = "test/repo1";

            const string repo1Digest1 = "sha256:repo1digest1";
            const string repo1Digest2 = "sha256:repo1digest2";

            ContainerRepository repo1 = CreateContainerRepository(
                repo1Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: repo1Name, digest: repo1Digest1, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(8))),
                    CreateArtifactManifestProperties(repositoryName: repo1Name, digest: repo1Digest2, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(9)))
                ]);

            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([repo1]);
            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);
            Mock<IAcrContentClient> acrContentClientMock = CreateAcrContentClientMock(repo1Name);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                acrClientFactory,
                CreateAcrContentClientFactory(AcrName, [acrContentClientMock]),
                Mock.Of<ILogger<CleanAcrImagesCommand>>(),
                Mock.Of<ILifecycleMetadataService>(),
                Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            acrContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest1));
            acrContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest2));
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo1Name), Times.Never);
        }

        [TestMethod]
        public async Task TimeLimitStopsBeforeNextBatch()
        {
            const string repoName = "test/repo";
            const string digest = "sha256:digest";

            ContainerRepository repo = CreateContainerRepository(
                repoName,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(
                        repositoryName: repoName,
                        digest: digest,
                        lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(8)))
                ]);

            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([repo]);
            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);
            Mock<IAcrContentClient> acrContentClientMock = CreateAcrContentClientMock(repoName);
            IAcrContentClientFactory acrContentClientFactory =
                CreateAcrContentClientFactory(AcrName, [acrContentClientMock]);

            CleanAcrImagesCommand command = new(
                acrClientFactory,
                acrContentClientFactory,
                Mock.Of<ILogger<CleanAcrImagesCommand>>(),
                Mock.Of<ILifecycleMetadataService>(),
                Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;
            command.Options.TimeLimitMinutes = 0;

            await command.ExecuteAsync();

            acrContentClientMock.Verify(o => o.DeleteManifestAsync(It.IsAny<string>()), Times.Never);
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task DeletesCompletedBatchBeforeEnumeratingNextPage()
        {
            const string repoName = "test/repo";
            ArtifactManifestProperties[] firstPage = Enumerable.Range(0, 250)
                .Select(index => CreateArtifactManifestProperties(
                    repositoryName: repoName,
                    digest: $"sha256:{index}",
                    lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(8))))
                .ToArray();
            ArtifactManifestProperties finalManifest = CreateArtifactManifestProperties(
                repositoryName: repoName,
                digest: "sha256:final",
                lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(8)));
            int deletedManifestCount = 0;

            Mock<IAcrContentClient> acrContentClientMock = CreateAcrContentClientMock(repoName);
            acrContentClientMock
                .Setup(o => o.DeleteManifestAsync(It.IsAny<string>()))
                .Callback(() => Interlocked.Increment(ref deletedManifestCount))
                .Returns(Task.CompletedTask);

            IEnumerable<Page<ArtifactManifestProperties>> GetManifestPages()
            {
                yield return Page<ArtifactManifestProperties>.FromValues(
                    firstPage,
                    continuationToken: "next",
                    Mock.Of<Response>());
                deletedManifestCount.ShouldBe(firstPage.Length);
                yield return Page<ArtifactManifestProperties>.FromValues(
                    [finalManifest],
                    continuationToken: null,
                    Mock.Of<Response>());
            }

            Mock<ContainerRepository> repositoryMock = new();
            repositoryMock.SetupGet(o => o.Name).Returns(repoName);
            repositoryMock
                .Setup(o => o.GetAllManifestPropertiesAsync(
                    It.IsAny<ArtifactManifestOrder>(),
                    It.IsAny<CancellationToken>()))
                .Returns(AsyncPageable<ArtifactManifestProperties>.FromPages(GetManifestPages()));

            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([repositoryMock.Object]);
            CleanAcrImagesCommand command = new(
                CreateAcrClientFactory(AcrName, acrClientMock.Object),
                CreateAcrContentClientFactory(AcrName, [acrContentClientMock]),
                Mock.Of<ILogger<CleanAcrImagesCommand>>(),
                Mock.Of<ILifecycleMetadataService>(),
                Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            deletedManifestCount.ShouldBe(firstPage.Length + 1);
        }

        [TestMethod]
        public async Task ProcessesRepositoriesSequentially()
        {
            const string repo1Name = "test/repo1";
            const string repo2Name = "test/repo2";
            const string repo1Digest = "sha256:repo1digest";
            const string repo2Digest = "sha256:repo2digest";

            ContainerRepository repo1 = CreateContainerRepository(
                repo1Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(
                        repositoryName: repo1Name,
                        digest: repo1Digest,
                        lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(8))),
                    CreateArtifactManifestProperties(
                        repositoryName: repo1Name,
                        digest: "sha256:repo1current",
                        lastUpdatedOn: DateTimeOffset.Now)
                ]);
            ContainerRepository repo2 = CreateContainerRepository(
                repo2Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(
                        repositoryName: repo2Name,
                        digest: repo2Digest,
                        lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(8))),
                    CreateArtifactManifestProperties(
                        repositoryName: repo2Name,
                        digest: "sha256:repo2current",
                        lastUpdatedOn: DateTimeOffset.Now)
                ]);

            Mock<IAcrContentClient> repo1ContentClientMock = CreateAcrContentClientMock(repo1Name);
            Mock<IAcrContentClient> repo2ContentClientMock = CreateAcrContentClientMock(repo2Name);
            TaskCompletionSource repo1DeleteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource allowRepo1Delete = new(TaskCreationOptions.RunContinuationsAsynchronously);
            repo1ContentClientMock
                .Setup(o => o.DeleteManifestAsync(repo1Digest))
                .Returns(async () =>
                {
                    repo1DeleteStarted.SetResult();
                    await allowRepo1Delete.Task;
                });

            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([repo1, repo2]);
            CleanAcrImagesCommand command = new(
                CreateAcrClientFactory(AcrName, acrClientMock.Object),
                CreateAcrContentClientFactory(AcrName, [repo1ContentClientMock, repo2ContentClientMock]),
                Mock.Of<ILogger<CleanAcrImagesCommand>>(),
                Mock.Of<ILifecycleMetadataService>(),
                Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            Task cleanupTask = command.ExecuteAsync();
            await repo1DeleteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            repo2ContentClientMock.Verify(o => o.DeleteManifestAsync(It.IsAny<string>()), Times.Never);

            allowRepo1Delete.SetResult();
            await cleanupTask;

            repo2ContentClientMock.Verify(o => o.DeleteManifestAsync(repo2Digest));
        }

        [TestMethod]
        public async Task TestRepos()
        {
            const string repo1Name = "test/repo1";
            const string repo2Name = "test/repo2";

            const string repo1Digest1 = "sha256:repo1digest1";
            const string repo1Digest2 = "sha256:repo1digest2";
            const string repo2Digest1 = "sha256:repo2digest1";
            const string repo2Digest2 = "sha256:repo2digest2";

            ContainerRepository repo1 = CreateContainerRepository(
                repo1Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: repo1Name, digest: repo1Digest1, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(8))),
                    CreateArtifactManifestProperties(repositoryName: repo1Name, digest: repo1Digest2, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(6)))
                ]);

            ContainerRepository repo2 = CreateContainerRepository(
                repo2Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: repo2Name, digest: repo2Digest1, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(1))),
                    CreateArtifactManifestProperties(repositoryName: repo2Name, digest: repo2Digest2, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31)))
                ]);


            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([repo1, repo2]);

            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);

            Mock<IAcrContentClient> repo1ContentClientMock = CreateAcrContentClientMock(repo1Name);
            Mock<IAcrContentClient> repo2ContentClientMock = CreateAcrContentClientMock(repo2Name);

            IAcrContentClientFactory acrContentClientFactory = CreateAcrContentClientFactory(AcrName, [repo1ContentClientMock, repo2ContentClientMock]);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                acrClientFactory, acrContentClientFactory, Mock.Of<ILogger<CleanAcrImagesCommand>>(), Mock.Of<ILifecycleMetadataService>(), Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest1));
            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest2), Times.Never);
            repo2ContentClientMock.Verify(o => o.DeleteManifestAsync(repo2Digest1), Times.Never);
            repo2ContentClientMock.Verify(o => o.DeleteManifestAsync(repo2Digest2));
        }

        /// <summary>
        /// Validates that images with EOL date older than specified age are deleted.
        /// </summary>
        [TestMethod]
        public async Task DeleteEolImages()
        {
            const string repo1Name = "test/repo1";

            const string repo1Digest1 = "sha256:digest1";
            const string repo1Digest2 = "sha256:digest2";
            const string repo1Digest3 = "sha256:digest3";
            const string annotationdigest = "annotationdigest";

            const int age = 30;

            ContainerRepository repo1 = CreateContainerRepository(
                repo1Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: repo1Name, digest: repo1Digest1, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(8)), tags: ["latest"], registryLoginServer: AcrName),
                    CreateArtifactManifestProperties(repositoryName: repo1Name, digest: repo1Digest2, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(9)), tags: ["latest"], registryLoginServer: AcrName),
                    CreateArtifactManifestProperties(repositoryName: repo1Name, digest: repo1Digest3, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(10)), tags: ["latest"], registryLoginServer: AcrName),
                    CreateArtifactManifestProperties(repositoryName: repo1Name, digest: annotationdigest, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(10)), registryLoginServer: AcrName)
                ]);

            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([repo1]);

            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);

            Mock<IAcrContentClient> repo1ContentClientMock = CreateAcrContentClientMock(repo1Name,
                imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { repo1Digest1, new ManifestQueryResult(string.Empty, []) },
                            { repo1Digest2, new ManifestQueryResult(string.Empty, []) },
                            { repo1Digest3, new ManifestQueryResult(string.Empty, []) },
                            { annotationdigest, new ManifestQueryResult(string.Empty, new JsonObject { { "subject", "" } }) }
                        });

            IAcrContentClientFactory acrContentClientFactory = CreateAcrContentClientFactory(AcrName, [repo1ContentClientMock]);

            Mock<ILifecycleMetadataService> lifecycleMetadataServiceMock = CreateLifecycleMetadataServiceMock(age, repo1Name);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                acrClientFactory, acrContentClientFactory, Mock.Of<ILogger<CleanAcrImagesCommand>>(), lifecycleMetadataServiceMock.Object, Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneEol;
            command.Options.Age = age;
            command.Options.TimeLimitMinutes = 30;

            await command.ExecuteAsync();

            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest1));
            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest2), Times.Never);
            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest3), Times.Never);
            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(annotationdigest), Times.Never);
        }

        [TestMethod]
        public async Task ExcludedImages()
        {
            const string publicRepo1Name = "public/dotnet/nightly/repo1";
            const string publicRepo2Name = "public/dotnet/nightly/repo2";

            const string repo1Digest1 = "sha256:repo1digest1";
            const string repo1Digest2 = "sha256:repo1digest2";
            const string repo2Digest3 = "sha256:repo1digest3";

            ContainerRepository publicRepo1 = CreateContainerRepository(
                publicRepo1Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: publicRepo1Name, digest: repo1Digest1, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(60))),
                    CreateArtifactManifestProperties(repositoryName: publicRepo1Name, digest: repo1Digest2, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(60)), tags: ["tag"])
                ]);

            ContainerRepository publicRepo2 = CreateContainerRepository(
                publicRepo2Name,
                CreateContainerRepositoryProperties(),
                [
                    CreateArtifactManifestProperties(repositoryName: publicRepo2Name, digest: repo2Digest3, lastUpdatedOn: DateTimeOffset.Now.Subtract(TimeSpan.FromDays(60)), tags: ["tag2"]),
                ]);

            Mock<IAcrClient> acrClientMock = CreateAcrClientMock([publicRepo1, publicRepo2]);

            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);

            Mock<IAcrContentClient> repo1ContentClient = CreateAcrContentClientMock(publicRepo1Name);
            Mock<IAcrContentClient> repo2ContentClient = CreateAcrContentClientMock(publicRepo2Name);

            IAcrContentClientFactory acrContentClientFactory = CreateAcrContentClientFactory(
                AcrName, [repo1ContentClient, repo2ContentClient]);

            CleanAcrImagesCommand command = new(
                acrClientFactory, acrContentClientFactory, Mock.Of<ILogger<CleanAcrImagesCommand>>(), Mock.Of<ILifecycleMetadataService>(), Microsoft.Extensions.Options.Options.Create(new PublishConfiguration()));
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "public/dotnet/nightly/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 30;
            command.Options.ImagesToExclude =
                [
                    $"{publicRepo1Name}@{repo1Digest2}",
                    $"{publicRepo2Name}:tag2"
                ];

            await command.ExecuteAsync();

            repo1ContentClient.Verify(o => o.DeleteManifestAsync(repo1Digest1));
            repo1ContentClient.Verify(o => o.DeleteManifestAsync(repo1Digest2), Times.Never);
            repo1ContentClient.Verify(o => o.RepositoryName);
            repo1ContentClient.VerifyNoOtherCalls();

            repo2ContentClient.Verify(o => o.DeleteManifestAsync(repo2Digest3), Times.Never);
            repo2ContentClient.Verify(o => o.RepositoryName);
            repo2ContentClient.VerifyNoOtherCalls();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(It.IsAny<string>()), Times.Never);
        }

        private Mock<ILifecycleMetadataService> CreateLifecycleMetadataServiceMock(int age, string repoName)
        {
            DateOnly dateToday = DateOnly.FromDateTime(DateTime.Now);
            Mock<ILifecycleMetadataService> lifecycleMetadataServiceMock = new();
            SetupIsDigestAnnotatedForEolMethod(lifecycleMetadataServiceMock, repoName, "sha256:digest1", true, dateToday.AddDays(-age - 1));
            SetupIsDigestAnnotatedForEolMethod(lifecycleMetadataServiceMock, repoName, "sha256:digest2", false, dateToday);
            SetupIsDigestAnnotatedForEolMethod(lifecycleMetadataServiceMock, repoName, "sha256:digest3", true, dateToday.AddDays(-age + 1));
            return lifecycleMetadataServiceMock;
        }

        private static void SetupIsDigestAnnotatedForEolMethod(Mock<ILifecycleMetadataService> lifecycleMetadataServiceMock, string repoName, string digest, bool digestAlreadyAnnotated, DateOnly eolDate)
        {
            string reference = $"{AcrName}/{repoName}@{digest}";

            Manifest manifest = null;
            if (digestAlreadyAnnotated)
            {
                manifest = new Manifest
                {
                    Annotations = new Dictionary<string, string>
                    {
                        { LifecycleMetadataService.EndOfLifeAnnotation, eolDate.ToString("yyyy-MM-dd") }
                    },
                    Reference = reference
                };
            }

            lifecycleMetadataServiceMock
                .Setup(o => o.IsDigestAnnotatedForEolAsync(reference, It.IsAny<CancellationToken>()))
                .ReturnsAsync(manifest);
        }
    }
}
