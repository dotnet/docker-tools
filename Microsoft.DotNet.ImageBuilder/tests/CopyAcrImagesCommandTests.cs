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

                CopyAcrImagesCommand command = new CopyAcrImagesCommand(azureManagementFactoryMock.Object);
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
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, "runtime")))
                );
                manifest.Registry = "mcr.microsoft.com";

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                RepoData runtimeRepo;

                RepoData[] repos = new RepoData[]
                {
                    runtimeRepo = new RepoData
                    {
                        Repo = "runtime",
                        Images = new SortedDictionary<string, ImageData>
                        {
                            {
                                dockerfileRelativePath,
                                new ImageData
                                {
                                    SimpleTags =
                                    {
                                        "tag1",
                                        "tag2"
                                    }
                                }
                            }
                        }
                    }
                };

                File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(repos));

                command.LoadManifest();
                await command.ExecuteAsync();

                IList<string> expectedTags = runtimeRepo.Images.First().Value.SimpleTags
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
