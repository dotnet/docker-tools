// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Acr;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class CleanAcrImagesCommandTest
    {
        [Fact]
        public async Task NonPublicRepos()
        {
            const string acrName = "myacr.azurecr.io";
            const string tenant = "mytenant";
            const string username = "fake user";
            const string password = "fake password";
            const string subscription = "my sub";
            const string resourceGroup = "group";

            const string nonPublicRepo1Name = "repo1";
            const string nonPublicRepo2Name = "repo2";

            Catalog catalog = new Catalog
            {
                RepositoryNames = new string[]
                {
                    nonPublicRepo1Name,
                    nonPublicRepo2Name
                }
            };

            Repository nonPublicRepo1 = new Repository
            {
                LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(14)),
                Name = nonPublicRepo1Name
            };

            Repository nonPublicRepo2 = new Repository
            {
                LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(16)),
                Name = nonPublicRepo2Name
            };

            Mock<IAcrClient> acrClientMock = new Mock<IAcrClient>();
            acrClientMock
                .Setup(o => o.GetCatalogAsync())
                .ReturnsAsync(catalog);
            acrClientMock
                .Setup(o => o.GetRepositoryAsync(nonPublicRepo1Name))
                .ReturnsAsync(nonPublicRepo1);
            acrClientMock
                .Setup(o => o.GetRepositoryAsync(nonPublicRepo2Name))
                .ReturnsAsync(nonPublicRepo2);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(nonPublicRepo2Name))
                .ReturnsAsync(new DeleteRepositoryResponse());

            Mock<IAcrClientFactory> acrClientFactoryMock = new Mock<IAcrClientFactory>();
            acrClientFactoryMock
                .Setup(o => o.CreateAsync(acrName, tenant, username, password))
                .ReturnsAsync(acrClientMock.Object);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                acrClientFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.Password = password;
            command.Options.Username = username;
            command.Options.Tenant = tenant;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(nonPublicRepo1Name), Times.Never);
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(nonPublicRepo2Name));
        }

        [Fact]
        public async Task PublicRepos()
        {
            const string acrName = "myacr.azurecr.io";
            const string tenant = "mytenant";
            const string username = "fake user";
            const string password = "fake password";
            const string subscription = "my sub";
            const string resourceGroup = "group";

            const string publicRepo1Name = "public/dotnet/core-nightly/repo1";
            const string publicRepo2Name = "public/dotnet/core/repo2";
            const string publicRepo3Name = "public/dotnet/core-nightly/repo3";

            Catalog catalog = new Catalog
            {
                RepositoryNames = new string[]
                {
                    publicRepo1Name,
                    publicRepo2Name,
                    publicRepo3Name
                }
            };

            const string repo1Digest1 = "sha256:repo1digest1";
            const string repo1Digest2 = "sha256:repo1digest2";

            RepositoryManifests repo1Manifests = new RepositoryManifests
            {
                RepositoryName = publicRepo1Name,
                Manifests = new Manifest[]
                {
                    new Manifest
                    {
                        Digest = repo1Digest1,
                        Tags = new string[0]
                    },
                    new Manifest
                    {
                        Digest = repo1Digest2,
                        Tags = new string[]
                        {
                            "tag"
                        }
                    }
                }
            };

            const string repo3Digest1 = "sha256:repo3digest1";
            const string repo3Digest2 = "sha256:repo3digest2";

            RepositoryManifests repo3Manifests = new RepositoryManifests
            {
                RepositoryName = publicRepo3Name,
                Manifests = new Manifest[]
                {
                    new Manifest
                    {
                        Digest = repo3Digest1,
                        Tags = new string[0]
                    },
                    new Manifest
                    {
                        Digest = repo3Digest2,
                        Tags = new string[0]
                    }
                }
            };

            Mock<IAcrClient> acrClientMock = new Mock<IAcrClient>();
            acrClientMock
                .Setup(o => o.GetCatalogAsync())
                .ReturnsAsync(catalog);
            foreach (string repoName in catalog.RepositoryNames)
            {
                acrClientMock
                    .Setup(o => o.GetRepositoryAsync(repoName))
                    .ReturnsAsync(new Repository { Name = repoName });
            }
            acrClientMock
                .Setup(o => o.GetRepositoryManifests(publicRepo1Name))
                .ReturnsAsync(repo1Manifests);
            acrClientMock
                .Setup(o => o.GetRepositoryManifests(publicRepo3Name))
                .ReturnsAsync(repo3Manifests);

            Mock<IAcrClientFactory> acrClientFactoryMock = new Mock<IAcrClientFactory>();
            acrClientFactoryMock
                .Setup(o => o.CreateAsync(acrName, tenant, username, password))
                .ReturnsAsync(acrClientMock.Object);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                acrClientFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.Password = password;
            command.Options.Username = username;
            command.Options.Tenant = tenant;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo1Name, repo1Digest1));
            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo1Name, repo1Digest2), Times.Never);
            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo2Name, It.IsAny<string>()), Times.Never);
            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo3Name, repo3Digest1));
            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo3Name, repo3Digest2));
        }
    }
}
