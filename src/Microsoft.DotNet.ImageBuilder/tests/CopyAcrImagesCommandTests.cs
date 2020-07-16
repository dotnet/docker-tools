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
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Rest.Azure;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;

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
                Mock<IRegistriesOperations> registriesOperationsMock = CreateRegistriesOperationsMock();
                IAzure azure = CreateAzureMock(registriesOperationsMock);
                Mock<IAzureManagementFactory> azureManagementFactoryMock = CreateAzureManagementFactoryMock(subscriptionId, azure);

                Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

                CopyAcrImagesCommand command = new CopyAcrImagesCommand(
                    azureManagementFactoryMock.Object, environmentServiceMock.Object, Mock.Of<ILoggerService>());
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
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "runtime" })))
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

                environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
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
                Mock<IRegistriesOperations> registriesOperationsMock = CreateRegistriesOperationsMock();
                IAzure azure = CreateAzureMock(registriesOperationsMock);
                Mock<IAzureManagementFactory> azureManagementFactoryMock = CreateAzureManagementFactoryMock(subscriptionId, azure);

                Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

                CopyAcrImagesCommand command = new CopyAcrImagesCommand(
                    azureManagementFactoryMock.Object, environmentServiceMock.Object, Mock.Of<ILoggerService>());
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
                                                osVersion: "Alpine 3.10"),
                                            CreatePlatform(
                                                PathHelper.NormalizePath(dockerfileRelativePath),
                                                simpleTags: new List<string>
                                                {
                                                    "tag2a"
                                                },
                                                osVersion: "Alpine 3.11")
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

                environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
            }
        }

        private static Mock<IAzureManagementFactory> CreateAzureManagementFactoryMock(string subscriptionId, IAzure azure)
        {
            Mock<IAzureManagementFactory> azureManagementFactoryMock = new Mock<IAzureManagementFactory>();
            azureManagementFactoryMock
                .Setup(o => o.CreateAzureManager(It.IsAny<AzureCredentials>(), subscriptionId))
                .Returns(azure);
            return azureManagementFactoryMock;
        }

        private static IAzure CreateAzureMock(Mock<IRegistriesOperations> registriesOperationsMock) =>
            Mock.Of<IAzure>(o => o.ContainerRegistries.Inner == registriesOperationsMock.Object);

        private static Mock<IRegistriesOperations> CreateRegistriesOperationsMock()
        {
            Mock<IRegistriesOperations> registriesOperationsMock = new Mock<IRegistriesOperations>();
            registriesOperationsMock
                .Setup(o => o.ImportImageWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ImportImageParametersInner>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureOperationResponse());
            return registriesOperationsMock;
        }

        private static bool VerifyImportImageParameters(ImportImageParametersInner parameters, IList<string> expectedTags)
        {
            return TestHelper.CompareLists(expectedTags, parameters.TargetTags);
        }
    }
}
