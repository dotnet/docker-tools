// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class CopyAcrImagesCommandTests
    {
        /// <summary>
        /// Verifies that image tags associated with a custom Dockerfile will by copied to ACR correctly.
        /// </summary>
        [Fact]
        public async Task CopyAcrImagesCommand_CustomDockerfileName()
        {
            const string subscriptionId = "my subscription";

            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                Mock<IRegistriesOperations> registriesOperationsMock = AzureHelper.CreateRegistriesOperationsMock();
                IAzure azure = AzureHelper.CreateAzureMock(registriesOperationsMock);
                Mock<IAzureManagementFactory> azureManagementFactoryMock =
                    AzureHelper.CreateAzureManagementFactoryMock(subscriptionId, azure);

                CopyAcrImagesCommand command = new CopyAcrImagesCommand(
                    Mock.Of<IDockerService>(), azureManagementFactoryMock.Object, Mock.Of<ILoggerService>());
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.Subscription = subscriptionId;
                command.Options.ResourceGroup = "my resource group";
                command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
                command.Options.ImageInfoPath = "image-info.json";

                const string runtimeRelativeDir = "1.0/runtime/os";
                Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile.custom");
                File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

                Manifest manifest = ManifestHelper.CreateManifest(
                    ManifestHelper.CreateRepo("runtime",
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "tag1", "tag2" })))
                );
                manifest.Registry = "mcr.microsoft.com";

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                RepoData runtimeRepo;

                ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
                {
                    Repos =
                    {
                        {
                            runtimeRepo = new RepoData
                            {
                                Repo = "runtime",
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                PathHelper.NormalizePath(dockerfileRelativePath),
                                                simpleTags: new List<string>
                                                {
                                                    "tag1",
                                                    "tag2"
                                                })
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

                command.LoadManifest();
                await command.ExecuteAsync();

                IList<string> expectedTags = runtimeRepo.Images.First().Platforms.First().SimpleTags
                    .Select(tag => $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:{tag}")
                    .ToList();

                foreach (string expectedTag in expectedTags)
                {
                    registriesOperationsMock
                        .Verify(o => o.ImportImageWithHttpMessagesAsync(
                            command.Options.ResourceGroup,
                            manifest.Registry,
                            It.Is<ImportImageParametersInner>(parameters =>
                                VerifyImportImageParameters(parameters, new List<string> { expectedTag })),
                            It.IsAny<Dictionary<string, List<string>>>(),
                            It.IsAny<CancellationToken>()));
                }
            }
        }

        /// <summary>
        /// Verifies that image tags associated with a Dockerfile that is shared by more than one platform are copied.
        /// </summary>
        [Fact]
        public async Task CopyAcrImagesCommand_SharedDockerfile()
        {
            const string subscriptionId = "my subscription";

            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                Mock<IRegistriesOperations> registriesOperationsMock = AzureHelper.CreateRegistriesOperationsMock();
                IAzure azure = AzureHelper.CreateAzureMock(registriesOperationsMock);
                Mock<IAzureManagementFactory> azureManagementFactoryMock =
                    AzureHelper.CreateAzureManagementFactoryMock(subscriptionId, azure);

                Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

                CopyAcrImagesCommand command = new CopyAcrImagesCommand(
                    Mock.Of<IDockerService>(), azureManagementFactoryMock.Object, Mock.Of<ILoggerService>());
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.Subscription = subscriptionId;
                command.Options.ResourceGroup = "my resource group";
                command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
                command.Options.ImageInfoPath = "image-info.json";

                const string runtimeRelativeDir = "1.0/runtime/os";
                Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile");
                File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

                Manifest manifest = ManifestHelper.CreateManifest(
                    ManifestHelper.CreateRepo("runtime",
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "tag1a", "tag1b" }, osVersion: "alpine3.10"),
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "tag2a" }, osVersion: "alpine3.11")))
                );
                manifest.Registry = "mcr.microsoft.com";

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                RepoData runtimeRepo;

                ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
                {
                    Repos =
                    {
                        {
                            runtimeRepo = new RepoData
                            {
                                Repo = "runtime",
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                PathHelper.NormalizePath(dockerfileRelativePath),
                                                simpleTags: new List<string>
                                                {
                                                    "tag1a",
                                                    "tag1b"
                                                },
                                                osVersion: "alpine3.10"),
                                            CreatePlatform(
                                                PathHelper.NormalizePath(dockerfileRelativePath),
                                                simpleTags: new List<string>
                                                {
                                                    "tag2a"
                                                },
                                                osVersion: "alpine3.11")
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

                command.LoadManifest();
                await command.ExecuteAsync();

                List<string> expectedTags = new List<string>
                {
                    $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag1a",
                    $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag1b",
                    $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag2a"
                };

                foreach (string expectedTag in expectedTags)
                {
                    registriesOperationsMock
                        .Verify(o => o.ImportImageWithHttpMessagesAsync(
                            command.Options.ResourceGroup,
                            manifest.Registry,
                            It.Is<ImportImageParametersInner>(parameters =>
                                VerifyImportImageParameters(parameters, new List<string> { expectedTag })),
                            It.IsAny<Dictionary<string, List<string>>>(),
                            It.IsAny<CancellationToken>()));
                }
            }
        }

        /// <summary>
        /// Verifies that image tags associated with a runtime-deps Dockerfiles that is shared by multiple versions.
        /// </summary>
        [Fact]
        public async Task CopyAcrImagesCommand_RuntimeDepsSharing()
        {
            const string subscriptionId = "my subscription";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IRegistriesOperations> registriesOperationsMock = AzureHelper.CreateRegistriesOperationsMock();
            IAzure azure = AzureHelper.CreateAzureMock(registriesOperationsMock);
            Mock<IAzureManagementFactory> azureManagementFactoryMock =
                AzureHelper.CreateAzureManagementFactoryMock(subscriptionId, azure);

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            CopyAcrImagesCommand command = new CopyAcrImagesCommand(
                Mock.Of<IDockerService>(), azureManagementFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.Subscription = subscriptionId;
            command.Options.ResourceGroup = "my resource group";
            command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
            command.Options.ImageInfoPath = "image-info.json";

            string dockerfileRelativePath = DockerfileHelper.CreateDockerfile("3.1/runtime-deps/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo("runtime-deps",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRelativePath, new string[] { "3.1" }, osVersion: "focal")
                        },
                        productVersion: "3.1"),
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRelativePath, new string[] { "5.0" }, osVersion: "focal")
                        },
                        productVersion: "5.0"))
            );
            manifest.Registry = "mcr.microsoft.com";

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            RepoData runtimeRepo;

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    {
                        runtimeRepo = new RepoData
                        {
                            Repo = "runtime-deps",
                            Images =
                            {
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfileRelativePath),
                                            simpleTags: new List<string>
                                            {
                                                "3.1"
                                            },
                                            osVersion: "focal")
                                    },
                                    ProductVersion = "3.1"
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfileRelativePath),
                                            simpleTags: new List<string>
                                            {
                                                "5.0"
                                            },
                                            osVersion: "focal")
                                    },
                                    ProductVersion = "5.0"
                                }
                            }
                        }
                    }
                }
            };

            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            await command.ExecuteAsync();

            List<string> expectedTags = new List<string>
            {
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:3.1",
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:5.0"
            };

            foreach (string expectedTag in expectedTags)
            {
                registriesOperationsMock
                    .Verify(o => o.ImportImageWithHttpMessagesAsync(
                        command.Options.ResourceGroup,
                        manifest.Registry,
                        It.Is<ImportImageParametersInner>(parameters =>
                            VerifyImportImageParameters(parameters, new List<string> { expectedTag })),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()));
            }
        }

        /// <summary>
        /// Verifies that image tags can be syndicated to another repo.
        /// </summary>
        [Fact]
        public async Task SyndicatedTags()
        {
            const string subscriptionId = "my subscription";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IRegistriesOperations> registriesOperationsMock = AzureHelper.CreateRegistriesOperationsMock();
            IAzure azure = AzureHelper.CreateAzureMock(registriesOperationsMock);
            Mock<IAzureManagementFactory> azureManagementFactoryMock =
                AzureHelper.CreateAzureManagementFactoryMock(subscriptionId, azure);

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            CopyAcrImagesCommand command = new CopyAcrImagesCommand(
                Mock.Of<IDockerService>(), azureManagementFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.Subscription = subscriptionId;
            command.Options.ResourceGroup = "my resource group";
            command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
            command.Options.ImageInfoPath = "image-info.json";

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile");
            File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

            Manifest manifest = ManifestHelper.CreateManifest(
                ManifestHelper.CreateRepo("runtime",
                    ManifestHelper.CreateImage(
                        ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "tag1", "tag2", "tag3" })))
            );
            manifest.Registry = "mcr.microsoft.com";

            const string syndicatedRepo2 = "runtime2";
            const string syndicatedRepo3 = "runtime3";

            Platform platform = manifest.Repos.First().Images.First().Platforms.First();
            platform.Tags["tag2"].Syndication = new TagSyndication
            {
                Repo = syndicatedRepo2,
            };
            platform.Tags["tag3"].Syndication = new TagSyndication
            {
                Repo = syndicatedRepo3,
                DestinationTags = new string[]
                {
                    "tag3a",
                    "tag3b"
                }
            };

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            RepoData runtimeRepo;

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    {
                        runtimeRepo = new RepoData
                        {
                            Repo = "runtime",
                            Images =
                            {
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfileRelativePath),
                                            simpleTags: new List<string>
                                            {
                                                "tag1",
                                                "tag2",
                                                "tag3"
                                            })
                                    }
                                }
                            }
                        }
                    }
                }
            };

            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            await command.ExecuteAsync();

            List<string> expectedTags = new List<string>
            {
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag1",
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag2",
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag3",
                $"{command.Options.RepoPrefix}{syndicatedRepo2}:tag2",
                $"{command.Options.RepoPrefix}{syndicatedRepo3}:tag3a",
                $"{command.Options.RepoPrefix}{syndicatedRepo3}:tag3b"
            };

            foreach (string expectedTag in expectedTags)
            {
                registriesOperationsMock
                    .Verify(o => o.ImportImageWithHttpMessagesAsync(
                        command.Options.ResourceGroup,
                        manifest.Registry,
                        It.Is<ImportImageParametersInner>(parameters =>
                            VerifyImportImageParameters(parameters, new List<string> { expectedTag })),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()));
            }
        }

        private static bool VerifyImportImageParameters(ImportImageParametersInner parameters, IList<string> expectedTags)
        {
            return TestHelper.CompareLists(expectedTags, parameters.TargetTags);
        }
    }
}
