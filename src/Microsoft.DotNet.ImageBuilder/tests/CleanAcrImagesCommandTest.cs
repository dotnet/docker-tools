// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        public async Task StagingRepos()
        {
            const string acrName = "myacr.azurecr.io";
            const string tenant = "mytenant";
            const string username = "fake user";
            const string password = "PLACEHOLDER";
            const string subscription = "my sub";
            const string resourceGroup = "group";

            const string stagingRepo1Name = "build-staging/repo1";
            const string stagingRepo2Name = "build-staging/repo2";

            Catalog catalog = new Catalog
            {
                RepositoryNames = new List<string>
                {
                    stagingRepo1Name,
                    stagingRepo2Name
                }
            };

            Repository nonPublicRepo1 = new Repository
            {
                LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(14)),
                Name = stagingRepo1Name
            };

            Repository nonPublicRepo2 = new Repository
            {
                LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(16)),
                Name = stagingRepo2Name
            };

            const string repo1Digest1 = "sha256:repo1digest1";

            RepositoryManifests repo1Manifests = new RepositoryManifests
            {
                RepositoryName = stagingRepo1Name,
                Manifests = new List<ManifestAttributes>
                {
                    new ManifestAttributes
                    {
                        Digest = repo1Digest1
                    }
                }
            };

            const string repo2Digest1 = "sha256:repo2digest1";

            RepositoryManifests repo2Manifests = new RepositoryManifests
            {
                RepositoryName = stagingRepo2Name,
                Manifests = new List<ManifestAttributes>
                {
                    new ManifestAttributes
                    {
                        Digest = repo2Digest1
                    }
                }
            };

            Mock<IAcrClient> acrClientMock = new Mock<IAcrClient>();
            acrClientMock
                .Setup(o => o.GetCatalogAsync())
                .ReturnsAsync(catalog);
            acrClientMock
                .Setup(o => o.GetRepositoryManifestsAsync(stagingRepo1Name))
                .ReturnsAsync(repo1Manifests);
            acrClientMock
                .Setup(o => o.GetRepositoryManifestsAsync(stagingRepo2Name))
                .ReturnsAsync(repo2Manifests);
            acrClientMock
                .Setup(o => o.GetRepositoryAsync(stagingRepo1Name))
                .ReturnsAsync(nonPublicRepo1);
            acrClientMock
                .Setup(o => o.GetRepositoryAsync(stagingRepo2Name))
                .ReturnsAsync(nonPublicRepo2);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(stagingRepo2Name))
                .ReturnsAsync(new DeleteRepositoryResponse());

            Mock<IAcrClientFactory> acrClientFactoryMock = new Mock<IAcrClientFactory>();
            acrClientFactoryMock
                .Setup(o => o.CreateAsync(acrName, tenant, username, password))
                .ReturnsAsync(acrClientMock.Object);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                Mock.Of<IDockerService>(), acrClientFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ServicePrincipal.Secret = password;
            command.Options.ServicePrincipal.ClientId = username;
            command.Options.ServicePrincipal.Tenant = tenant;
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
            const string tenant = "mytenant";
            const string username = "fake user";
            const string password = "PLACEHOLDER";
            const string subscription = "my sub";
            const string resourceGroup = "group";

            const string publicRepo1Name = "public/dotnet/core-nightly/repo1";
            const string publicRepo2Name = "public/dotnet/core/repo2";
            const string publicRepo3Name = "public/dotnet/core-nightly/repo3";
            const string publicRepo4Name = "public/dotnet/nightly/repo4";

            Catalog catalog = new Catalog
            {
                RepositoryNames = new List<string>
                {
                    publicRepo1Name,
                    publicRepo2Name,
                    publicRepo3Name,
                    publicRepo4Name
                }
            };

            const string repo1Digest1 = "sha256:repo1digest1";
            const string repo1Digest2 = "sha256:repo1digest2";

            RepositoryManifests repo1Manifests = new RepositoryManifests
            {
                RepositoryName = publicRepo1Name,
                Manifests = new List<ManifestAttributes>
                {
                    new ManifestAttributes
                    {
                        Digest = repo1Digest1,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(1)),
                        Tags = new string[0]
                    },
                    new ManifestAttributes
                    {
                        Digest = repo1Digest2,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(31)),
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
                Manifests = new List<ManifestAttributes>
                {
                    new ManifestAttributes
                    {
                        Digest = repo3Digest1,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(29)),
                        Tags = new string[0]
                    },
                    new ManifestAttributes
                    {
                        Digest = repo3Digest2,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(31)),
                        Tags = new string[0]
                    }
                }
            };

            const string repo4Digest1 = "sha256:repo4digest1";

            RepositoryManifests repo4Manifests = new RepositoryManifests
            {
                RepositoryName = publicRepo4Name,
                Manifests = new List<ManifestAttributes>
                {
                    new ManifestAttributes
                    {
                        Digest = repo4Digest1,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(60)),
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
                .Setup(o => o.GetRepositoryManifestsAsync(publicRepo1Name))
                .ReturnsAsync(repo1Manifests);
            acrClientMock
                .Setup(o => o.GetRepositoryManifestsAsync(publicRepo3Name))
                .ReturnsAsync(repo3Manifests);
            acrClientMock
                .Setup(o => o.GetRepositoryManifestsAsync(publicRepo4Name))
                .ReturnsAsync(repo4Manifests);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(publicRepo4Name))
                .ReturnsAsync(new DeleteRepositoryResponse());

            Mock<IAcrClientFactory> acrClientFactoryMock = new Mock<IAcrClientFactory>();
            acrClientFactoryMock
                .Setup(o => o.CreateAsync(acrName, tenant, username, password))
                .ReturnsAsync(acrClientMock.Object);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                Mock.Of<IDockerService>(), acrClientFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ServicePrincipal.Secret = password;
            command.Options.ServicePrincipal.ClientId = username;
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;
            command.Options.RepoName = "public/dotnet/*nightly/*";
            command.Options.Action = CleanAcrImagesAction.PruneDangling;
            command.Options.Age = 30;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo1Name, repo1Digest1), Times.Never);
            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo1Name, repo1Digest2), Times.Never);
            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo2Name, It.IsAny<string>()), Times.Never);
            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo3Name, repo3Digest1), Times.Never);
            acrClientMock.Verify(o => o.DeleteManifestAsync(publicRepo3Name, repo3Digest2));
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(publicRepo4Name));
        }

        /// <summary>
        /// Validates that an empty test repo will be deleted.
        /// </summary>
        [Fact]
        public async Task DeleteEmptyTestRepo()
        {
            const string acrName = "myacr.azurecr.io";
            const string tenant = "mytenant";
            const string username = "fake user";
            const string password = "PLACEHOLDER";
            const string subscription = "my sub";
            const string resourceGroup = "group";

            const string repo1Name = "test/repo1";
            const string repo2Name = "test/repo2";

            Catalog catalog = new Catalog
            {
                RepositoryNames = new List<string>
                {
                    repo1Name,
                    repo2Name
                }
            };

            const string repo1Digest1 = "sha256:repo1digest1";

            RepositoryManifests repo1Manifests = new RepositoryManifests
            {
                RepositoryName = repo1Name,
                Manifests = new List<ManifestAttributes>
                {
                    new ManifestAttributes
                    {
                        Digest = repo1Digest1,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(1))
                    }
                }
            };

            RepositoryManifests repo2Manifests = new RepositoryManifests
            {
                RepositoryName = repo2Name,
                Manifests = new List<ManifestAttributes>()
            };

            Mock<IAcrClient> acrClientMock = new Mock<IAcrClient>();
            acrClientMock
                .Setup(o => o.GetCatalogAsync())
                .ReturnsAsync(catalog);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(repo2Name))
                .ReturnsAsync(new DeleteRepositoryResponse());

            foreach (string repoName in catalog.RepositoryNames)
            {
                acrClientMock
                    .Setup(o => o.GetRepositoryAsync(repoName))
                    .ReturnsAsync(new Repository { Name = repoName });
            }
            acrClientMock
                .Setup(o => o.GetRepositoryManifestsAsync(repo1Name))
                .ReturnsAsync(repo1Manifests);
            acrClientMock
                .Setup(o => o.GetRepositoryManifestsAsync(repo2Name))
                .ReturnsAsync(repo2Manifests);

            Mock<IAcrClientFactory> acrClientFactoryMock = new Mock<IAcrClientFactory>();
            acrClientFactoryMock
                .Setup(o => o.CreateAsync(acrName, tenant, username, password))
                .ReturnsAsync(acrClientMock.Object);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                Mock.Of<IDockerService>(), acrClientFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ServicePrincipal.Secret = password;
            command.Options.ServicePrincipal.ClientId = username;
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo1Name), Times.Never);
            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo2Name));
            acrClientMock.Verify(o => o.DeleteManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Validates that a test repo consisting of only expired images will result in the entire repo being deleted.
        /// </summary>
        [Fact]
        public async Task DeleteAllExpiredImagesTestRepo()
        {
            const string acrName = "myacr.azurecr.io";
            const string tenant = "mytenant";
            const string username = "fake user";
            const string password = "PLACEHOLDER";
            const string subscription = "my sub";
            const string resourceGroup = "group";

            const string repo1Name = "test/repo1";

            Catalog catalog = new Catalog
            {
                RepositoryNames = new List<string>
                {
                    repo1Name
                }
            };

            const string repo1Digest1 = "sha256:repo1digest1";
            const string repo1Digest2 = "sha256:repo1digest2";

            RepositoryManifests repo1Manifests = new RepositoryManifests
            {
                RepositoryName = repo1Name,
                Manifests = new List<ManifestAttributes>
                {
                    new ManifestAttributes
                    {
                        Digest = repo1Digest1,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(8))
                    },
                    new ManifestAttributes
                    {
                        Digest = repo1Digest2,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(9))
                    }
                }
            };

            Mock<IAcrClient> acrClientMock = new Mock<IAcrClient>();
            acrClientMock
                .Setup(o => o.GetCatalogAsync())
                .ReturnsAsync(catalog);
            acrClientMock
                .Setup(o => o.DeleteRepositoryAsync(repo1Name))
                .ReturnsAsync(new DeleteRepositoryResponse());

            foreach (string repoName in catalog.RepositoryNames)
            {
                acrClientMock
                    .Setup(o => o.GetRepositoryAsync(repoName))
                    .ReturnsAsync(new Repository { Name = repoName });
            }
            acrClientMock
                .Setup(o => o.GetRepositoryManifestsAsync(repo1Name))
                .ReturnsAsync(repo1Manifests);

            Mock<IAcrClientFactory> acrClientFactoryMock = new Mock<IAcrClientFactory>();
            acrClientFactoryMock
                .Setup(o => o.CreateAsync(acrName, tenant, username, password))
                .ReturnsAsync(acrClientMock.Object);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                Mock.Of<IDockerService>(), acrClientFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ServicePrincipal.Secret = password;
            command.Options.ServicePrincipal.ClientId = username;
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteRepositoryAsync(repo1Name));
            acrClientMock.Verify(o => o.DeleteManifestAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task TestRepos()
        {
            const string acrName = "myacr.azurecr.io";
            const string tenant = "mytenant";
            const string username = "fake user";
            const string password = "PLACEHOLDER";
            const string subscription = "my sub";
            const string resourceGroup = "group";

            const string repo1Name = "test/repo1";
            const string repo2Name = "test/repo2";

            Catalog catalog = new Catalog
            {
                RepositoryNames = new List<string>
                {
                    repo1Name,
                    repo2Name
                }
            };

            const string repo1Digest1 = "sha256:repo1digest1";
            const string repo1Digest2 = "sha256:repo1digest2";

            RepositoryManifests repo1Manifests = new RepositoryManifests
            {
                RepositoryName = repo1Name,
                Manifests = new List<ManifestAttributes>
                {
                    new ManifestAttributes
                    {
                        Digest = repo1Digest1,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(8))
                    },
                    new ManifestAttributes
                    {
                        Digest = repo1Digest2,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(6))
                    }
                }
            };

            const string repo2Digest1 = "sha256:repo2digest1";
            const string repo2Digest2 = "sha256:repo2digest2";

            RepositoryManifests repo2Manifests = new RepositoryManifests
            {
                RepositoryName = repo2Name,
                Manifests = new List<ManifestAttributes>
                {
                    new ManifestAttributes
                    {
                        Digest = repo2Digest1,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(1))
                    },
                    new ManifestAttributes
                    {
                        Digest = repo2Digest2,
                        LastUpdateTime = DateTime.Now.Subtract(TimeSpan.FromDays(31))
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
                .Setup(o => o.GetRepositoryManifestsAsync(repo1Name))
                .ReturnsAsync(repo1Manifests);
            acrClientMock
                .Setup(o => o.GetRepositoryManifestsAsync(repo2Name))
                .ReturnsAsync(repo2Manifests);

            Mock<IAcrClientFactory> acrClientFactoryMock = new Mock<IAcrClientFactory>();
            acrClientFactoryMock
                .Setup(o => o.CreateAsync(acrName, tenant, username, password))
                .ReturnsAsync(acrClientMock.Object);

            CleanAcrImagesCommand command = new CleanAcrImagesCommand(
                Mock.Of<IDockerService>(), acrClientFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Subscription = subscription;
            command.Options.ServicePrincipal.Secret = password;
            command.Options.ServicePrincipal.ClientId = username;
            command.Options.ServicePrincipal.Tenant = tenant;
            command.Options.ResourceGroup = resourceGroup;
            command.Options.RegistryName = acrName;
            command.Options.RepoName = "test/*";
            command.Options.Action = CleanAcrImagesAction.PruneAll;
            command.Options.Age = 7;

            await command.ExecuteAsync();

            acrClientMock.Verify(o => o.DeleteManifestAsync(repo1Name, repo1Digest1));
            acrClientMock.Verify(o => o.DeleteManifestAsync(repo1Name, repo1Digest2), Times.Never);
            acrClientMock.Verify(o => o.DeleteManifestAsync(repo2Name, repo2Digest1), Times.Never);
            acrClientMock.Verify(o => o.DeleteManifestAsync(repo2Name, repo2Digest2));
        }
    }
}
