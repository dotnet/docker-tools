// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Containers.ContainerRegistry;
using Azure.Core;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class CleanAcrImagesCommandTest
    {
        [Fact]
        public async Task StagingRepos()
        {
            const string acrName = "myacr.azurecr.io";
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

            Mock<IContainerRegistryClient> acrClientMock = CreateContainerRegistryClientMock([nonPublicRepo1, nonPublicRepo2]);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(stagingRepo2Name))
                .Returns(Task.CompletedTask);

            IContainerRegistryClientFactory acrClientFactory = CreateContainerRegistryClientFactory(acrName, acrClientMock.Object);

            CleanAcrImagesCommand command = new(
                acrClientFactory, Mock.Of<IContainerRegistryContentClientFactory>(), Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;
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
            const string acrName = "myacr.azurecr.io";
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

            Mock<IContainerRegistryClient> acrClientMock = CreateContainerRegistryClientMock([publicRepo1, publicRepo2, publicRepo3, publicRepo4]);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(publicRepo4Name))
                .Returns(Task.CompletedTask);

            IContainerRegistryClientFactory acrClientFactory = CreateContainerRegistryClientFactory(acrName, acrClientMock.Object);

            Mock<IContainerRegistryContentClient> repo1ContentClient = CreateContainerRegistryContentClientMock(publicRepo1Name);
            Mock<IContainerRegistryContentClient> repo2ContentClient = CreateContainerRegistryContentClientMock(publicRepo2Name);
            Mock<IContainerRegistryContentClient> repo3ContentClient = CreateContainerRegistryContentClientMock(publicRepo3Name);
            Mock<IContainerRegistryContentClient> repo4ContentClient = CreateContainerRegistryContentClientMock(publicRepo4Name);

            IContainerRegistryContentClientFactory acrContentClientFactory = CreateContainerRegistryContentClientFactory(
                acrName, [repo1ContentClient, repo2ContentClient, repo3ContentClient, repo4ContentClient]);

            CleanAcrImagesCommand command = new(
                acrClientFactory, acrContentClientFactory, Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;
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
            const string acrName = "myacr.azurecr.io";
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

            Mock<IContainerRegistryClient> acrClientMock = CreateContainerRegistryClientMock([repo1, repo2]);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(repo2Name))
                .Returns(Task.CompletedTask);

            IContainerRegistryClientFactory acrClientFactory = CreateContainerRegistryClientFactory(acrName, acrClientMock.Object);

            CleanAcrImagesCommand command = new(
                acrClientFactory, Mock.Of<IContainerRegistryContentClientFactory>(), Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;
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
            const string acrName = "myacr.azurecr.io";
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

            Mock<IContainerRegistryClient> acrClientMock = CreateContainerRegistryClientMock([repo1]);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(repo1Name))
                .Returns(Task.CompletedTask);

            IContainerRegistryClientFactory acrClientFactory = CreateContainerRegistryClientFactory(acrName, acrClientMock.Object);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                acrClientFactory, Mock.Of<IContainerRegistryContentClientFactory>(), Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo1Name));
        }

        [Fact]
        public async Task TestRepos()
        {
            const string acrName = "myacr.azurecr.io";
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


            Mock<IContainerRegistryClient> acrClientMock = CreateContainerRegistryClientMock([repo1, repo2]);

            IContainerRegistryClientFactory acrClientFactory = CreateContainerRegistryClientFactory(acrName, acrClientMock.Object);

            Mock<IContainerRegistryContentClient> repo1ContentClientMock = CreateContainerRegistryContentClientMock(repo1Name);
            Mock<IContainerRegistryContentClient> repo2ContentClientMock = CreateContainerRegistryContentClientMock(repo2Name);

            IContainerRegistryContentClientFactory acrContentClientFactory = CreateContainerRegistryContentClientFactory(acrName, [repo1ContentClientMock, repo2ContentClientMock]);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                acrClientFactory, acrContentClientFactory, Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest1));
            repo1ContentClientMock.Verify(o => o.DeleteManifestAsync(repo1Digest2), Times.Never);
            repo2ContentClientMock.Verify(o => o.DeleteManifestAsync(repo2Digest1), Times.Never);
            repo2ContentClientMock.Verify(o => o.DeleteManifestAsync(repo2Digest2));
        }

        private static Mock<IContainerRegistryContentClient> CreateContainerRegistryContentClientMock(string repositoryName)
        {
            Mock<IContainerRegistryContentClient> acrClientContentMock = new();
            acrClientContentMock.SetupGet(o => o.RepositoryName).Returns(repositoryName);
            return acrClientContentMock;
        }

        private static Mock<IContainerRegistryClient> CreateContainerRegistryClientMock(IEnumerable<ContainerRepository> repositories)
        {
            Mock<IContainerRegistryClient> acrClientMock = new();

            IAsyncEnumerable<string> repositoryNames = repositories
                .Select(repo => repo.Name)
                .ToAsyncEnumerable();

            acrClientMock
                .Setup(o => o.GetRepositoryNames())
                .Returns(repositoryNames);

            foreach (ContainerRepository repo in repositories)
            {
                acrClientMock
                    .Setup(o => o.GetRepository(repo.Name))
                    .Returns(repo);
            }

            return acrClientMock;
        }

        private static IContainerRegistryClientFactory CreateContainerRegistryClientFactory(string acrName, IContainerRegistryClient acrClient)
        {
            Mock<IContainerRegistryClientFactory> acrClientFactoryMock = new();
            acrClientFactoryMock
                .Setup(o => o.Create(acrName, It.IsAny<TokenCredential>()))
                .Returns(acrClient);
            return acrClientFactoryMock.Object;
        }

        private static IContainerRegistryContentClientFactory CreateContainerRegistryContentClientFactory(
            string acrName, IEnumerable<Mock<IContainerRegistryContentClient>> acrContentClients)
        {
            Mock<IContainerRegistryContentClientFactory> acrContentClientFactoryMock = new();
            foreach (Mock<IContainerRegistryContentClient> clientMock in acrContentClients)
            {
                acrContentClientFactoryMock
                    .Setup(o => o.Create(acrName, clientMock.Object.RepositoryName, It.IsAny<TokenCredential>()))
                    .Returns(clientMock.Object);
            }
            
            return acrContentClientFactoryMock.Object;
        }

        private static ContainerRepository CreateContainerRepository(
            string repoName, ContainerRepositoryProperties repositoryProperties, ArtifactManifestProperties[] manifestProperties)
        {
            Mock<ContainerRepository> nonPublicRepo1Mock = new();
            nonPublicRepo1Mock.SetupGet(o => o.Name).Returns(repoName);
            nonPublicRepo1Mock
                .Setup(o => o.GetProperties(It.IsAny<CancellationToken>()))
                .Returns(CreateAzureResponse(repositoryProperties));
            nonPublicRepo1Mock
                .Setup(o => o.GetAllManifestProperties(It.IsAny<ArtifactManifestOrder>(), It.IsAny<CancellationToken>()))
                .Returns(new PageableMock<ArtifactManifestProperties>(manifestProperties));
            return nonPublicRepo1Mock.Object;
        }

        private static Response<T> CreateAzureResponse<T>(T obj)
            where T : class
        {
            Mock<Response<T>> response = new();
            response.SetupGet(o => o.Value).Returns(obj);
            return response.Object;
        }

        private class PageableMock<T>(IEnumerable<T> items) : Pageable<T>
        {
            private readonly IEnumerable<T> _items = items;

            public override IEnumerable<Page<T>> AsPages(string continuationToken = null, int? pageSizeHint = null) =>
                [new PageMock<T>(_items)];
        }

        private class PageMock<T>(IEnumerable<T> items) : Page<T>
        {
            private readonly IEnumerable<T> _items = items;

            public override IReadOnlyList<T> Values => _items.ToList();

            public override string ContinuationToken => throw new NotImplementedException();

            public override Response GetRawResponse() => throw new NotImplementedException();
        }

        private static ContainerRepositoryProperties CreateContainerRepositoryProperties(
            string registryLoginServer = "",
            string name = "",
            DateTimeOffset createdOn = default,
            DateTimeOffset lastUpdatedOn = default,
            int manifestCount = 0,
            int tagCount = 0)
        {
            // Have to use reflection here because the state we need to set is not settable from a public interface.
            // This is configured to invoke this constructor:
            // https://github.com/Azure/azure-sdk-for-net/blob/3d9f007d34562731419932dd987074662a3c2c1f/sdk/containerregistry/Azure.Containers.ContainerRegistry/src/Generated/Models/ContainerRepositoryProperties.cs#L22
            // IMPORTANT: The signature of this method must match the signature of the constructor being invoked.

            object[] args =
                [
                    registryLoginServer,
                    name,
                    createdOn,
                    lastUpdatedOn,
                    manifestCount,
                    tagCount
                ];

            MethodInfo thisMethod = typeof(CleanAcrImagesCommandTest).GetMethod(nameof(CreateContainerRepositoryProperties), BindingFlags.Static | BindingFlags.NonPublic);

            ConstructorInfo ctor = typeof(ContainerRepositoryProperties).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                thisMethod.GetParameters().Select(param => param.ParameterType).ToArray());
            return (ContainerRepositoryProperties)ctor.Invoke(args);
        }

        private static ArtifactManifestProperties CreateArtifactManifestProperties(
            string registryLoginServer = "",
            string repositoryName = "",
            string digest = "",
            long? sizeInBytes = null,
            DateTimeOffset createdOn = default,
            DateTimeOffset lastUpdatedOn = default,
            ArtifactArchitecture? architecture = null,
            ArtifactOperatingSystem? operatingSystem = null,
            IReadOnlyList<ArtifactManifestPlatform> relatedArtifacts = null,
            IReadOnlyList<string> tags = null,
            bool? canDelete = null,
            bool? canWrite = null,
            bool? canList = null,
            bool? canRead = null)
        {
            // Have to use reflection here because the state we need to set is not settable from a public interface.
            // This is configured to invoke this constructor:
            // https://github.com/Azure/azure-sdk-for-net/blob/3d9f007d34562731419932dd987074662a3c2c1f/sdk/containerregistry/Azure.Containers.ContainerRegistry/src/Generated/Models/ArtifactManifestProperties.cs#L44
            // IMPORTANT: The signature of this method must match the signature of the constructor being invoked.

            if (tags is null)
            {
                tags = new List<string>();
            }

            object[] args =
                [
                    registryLoginServer,
                    repositoryName,
                    digest,
                    sizeInBytes,
                    createdOn,
                    lastUpdatedOn,
                    architecture,
                    operatingSystem,
                    relatedArtifacts,
                    tags,
                    canDelete,
                    canWrite,
                    canList,
                    canRead
                ];

            MethodInfo thisMethod = typeof(CleanAcrImagesCommandTest).GetMethod(nameof(CreateArtifactManifestProperties), BindingFlags.Static | BindingFlags.NonPublic);

            ConstructorInfo ctor = typeof(ArtifactManifestProperties).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                thisMethod.GetParameters().Select(param => param.ParameterType).ToArray());
            return (ArtifactManifestProperties)ctor.Invoke(args);
        }
    }
}
