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

using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ContainerRegistryHelper;

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
                acrClientFactory, Mock.Of<IContainerRegistryContentClientFactory>(), Mock.Of<ILoggerService>(), Mock.Of<IAzureTokenCredentialProvider>());
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
                acrClientFactory, acrContentClientFactory, Mock.Of<ILoggerService>(), Mock.Of<IAzureTokenCredentialProvider>());
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
                acrClientFactory, Mock.Of<IContainerRegistryContentClientFactory>(), Mock.Of<ILoggerService>(), Mock.Of<IAzureTokenCredentialProvider>());
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
                acrClientFactory, Mock.Of<IContainerRegistryContentClientFactory>(), Mock.Of<ILoggerService>(), Mock.Of<IAzureTokenCredentialProvider>());
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
                acrClientFactory, acrContentClientFactory, Mock.Of<ILoggerService>(), Mock.Of<IAzureTokenCredentialProvider>());
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
    }
}
