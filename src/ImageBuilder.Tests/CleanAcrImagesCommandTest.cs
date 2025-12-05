// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Moq;
using Xunit;

using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ContainerRegistryHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class CleanAcrImagesCommandTest
    {
        private const string AcrName = "myacr.azurecr.io";

        [Fact]
        public async Task StagingRepos()
        {
            const string subscription = "my sub";
            const string resourceGroup = "group";

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
                acrClientFactory, Mock.Of<IAcrContentClientFactory>(), Mock.Of<ILoggerService>(), Mock.Of<ILifecycleMetadataService>(), Mock.Of<IRegistryCredentialsProvider>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "build-staging/*";
            command.Options.Action = CleanAcrImagesAction.Delete;
            command.Options.Age = 15;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(stagingRepo1Name), Times.Never);
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(stagingRepo2Name));
        }

        [Fact]
        public async Task PublicNightlyRepos()
        {
            const string subscription = "my sub";
            const string resourceGroup = "group";

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
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(publicRepo4Name))
                .Returns(Task.CompletedTask);

            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);

            Mock<IAcrContentClient> repo1ContentClient = CreateAcrContentClientMock(publicRepo1Name);
            Mock<IAcrContentClient> repo2ContentClient = CreateAcrContentClientMock(publicRepo2Name);
            Mock<IAcrContentClient> repo3ContentClient = CreateAcrContentClientMock(publicRepo3Name);
            Mock<IAcrContentClient> repo4ContentClient = CreateAcrContentClientMock(publicRepo4Name);

            IAcrContentClientFactory acrContentClientFactory = CreateAcrContentClientFactory(
                AcrName, [repo1ContentClient, repo2ContentClient, repo3ContentClient, repo4ContentClient]);

            CleanAcrImagesCommand command = new(
                acrClientFactory, acrContentClientFactory, Mock.Of<ILoggerService>(), Mock.Of<ILifecycleMetadataService>(), Mock.Of<IRegistryCredentialsProvider>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
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
            repo4ContentClient.Verify(o => o.DeleteManifestAsync(It.IsAny<string>()), Times.Never);
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(publicRepo4Name));
        }

        /// <summary>
        /// Validates that an empty test repo will be deleted.
        /// </summary>
        [Fact]
        public async Task DeleteEmptyTestRepo()
        {
            const string subscription = "my sub";
            const string resourceGroup = "group";

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
                acrClientFactory, Mock.Of<IAcrContentClientFactory>(), Mock.Of<ILoggerService>(), Mock.Of<ILifecycleMetadataService>(), Mock.Of<IRegistryCredentialsProvider>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo1Name), Times.Never);
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo2Name));
        }

        /// <summary>
        /// Validates that a test repo consisting of only expired images will result in the entire repo being deleted.
        /// </summary>
        [Fact]
        public async Task DeleteAllExpiredImagesTestRepo()
        {
            const string subscription = "my sub";
            const string resourceGroup = "group";

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
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(repo1Name))
                .Returns(Task.CompletedTask);

            IAcrClientFactory acrClientFactory = CreateAcrClientFactory(AcrName, acrClientMock.Object);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                acrClientFactory, Mock.Of<IAcrContentClientFactory>(), Mock.Of<ILoggerService>(), Mock.Of<ILifecycleMetadataService>(), Mock.Of<IRegistryCredentialsProvider>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo1Name));
        }

        [Fact]
        public async Task TestRepos()
        {
            const string subscription = "my sub";
            const string resourceGroup = "group";

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
                acrClientFactory, acrContentClientFactory, Mock.Of<ILoggerService>(), Mock.Of<ILifecycleMetadataService>(), Mock.Of<IRegistryCredentialsProvider>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
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
        [Fact]
        public async Task DeleteEolImages()
        {
            const string subscription = "my sub";
            const string resourceGroup = "group";

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
                acrClientFactory, acrContentClientFactory, Mock.Of<ILoggerService>(), lifecycleMetadataServiceMock.Object, Mock.Of<IRegistryCredentialsProvider>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = AcrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneEol;
            command.Options.Age = age;

            await command.ExecuteAsync();

            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest1));
            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest2), Times.Never);
            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest3), Times.Never);
            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(annotationdigest), Times.Never);
        }

        [Fact]
        public async Task ExcludedImages()
        {
            const string subscription = "my sub";
            const string resourceGroup = "group";

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
                acrClientFactory, acrContentClientFactory, Mock.Of<ILoggerService>(), Mock.Of<ILifecycleMetadataService>(), Mock.Of<IRegistryCredentialsProvider>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
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
                .Setup(o => o.IsDigestAnnotatedForEol(reference, It.IsAny<ILoggerService>(), It.IsAny<bool>(), out manifest))
                .Returns(digestAlreadyAnnotated);
        }
    }
}
