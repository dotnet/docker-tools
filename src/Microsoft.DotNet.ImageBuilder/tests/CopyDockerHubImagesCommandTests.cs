// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;

using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class CopyDockerHubImagesCommandTests
    {
        [Fact]
        public async Task MultipleBaseTags()
        {
            const string subscriptionId = "my subscription";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IRegistriesOperations> registriesOperationsMock = AzureHelper.CreateRegistriesOperationsMock();
            IAzure azure = AzureHelper.CreateAzureMock(registriesOperationsMock);
            Mock<IAzureManagementFactory> azureManagementFactoryMock =
                AzureHelper.CreateAzureManagementFactoryMock(subscriptionId, azure);

            Mock<IEnvironmentService> environmentServiceMock = new Mock<IEnvironmentService>();

            CopyDockerHubBaseImagesCommand command = new CopyDockerHubBaseImagesCommand(
                Mock.Of<IDockerService>(), azureManagementFactoryMock.Object, Mock.Of<ILoggerService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.Subscription = subscriptionId;
            command.Options.ResourceGroup = "my resource group";
            command.Options.RepoPrefix = "custom-repo/";

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile.custom");
            File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

            const string registry = "mcr.microsoft.com";

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/os/arm32v7", tempFolderContext, "arm32v7/base:tag"),
                            new string[] { "arm32v7" }),
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/os/amd64", tempFolderContext, "amd64/base:tag"),
                            new string[] { "amd64" }))),
                CreateRepo("aspnet",
                    CreateImage(
                        CreatePlatformWithRepoBuildArg(
                            CreateDockerfile("1.0/aspnet/os/arm32v7", tempFolderContext, "$REPO:arm32v7"),
                            $"{registry}/{command.Options.RepoPrefix}aspnet",
                            new string[] { "arm32v7" }),
                        CreatePlatformWithRepoBuildArg(
                            CreateDockerfile("1.0/aspnet/os/amd64", tempFolderContext, "$REPO:amd64"),
                            $"{registry}/{command.Options.RepoPrefix}aspnet",
                            new string[] { "amd64" })))
            );
            manifest.Registry = registry;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            var expectedTagInfos = new (string SourceImage, string TargetTag)[]
            {
                ( "arm32v7/base:tag", $"{command.Options.RepoPrefix}arm32v7/base:tag" ),
                ( "amd64/base:tag", $"{command.Options.RepoPrefix}amd64/base:tag" )
            };

            foreach (var expectedTagInfo in expectedTagInfos)
            {
                registriesOperationsMock
                    .Verify(o => o.ImportImageWithHttpMessagesAsync(
                        command.Options.ResourceGroup,
                        manifest.Registry,
                        It.Is<ImportImageParametersInner>(parameters =>
                            parameters.Source.RegistryUri == "docker.io" &&
                            parameters.Source.SourceImage == expectedTagInfo.SourceImage &&
                            TestHelper.CompareLists(new List<string> { expectedTagInfo.TargetTag }, parameters.TargetTags)),
                        It.IsAny<Dictionary<string, List<string>>>(),
                        It.IsAny<CancellationToken>()));
            }

            registriesOperationsMock.VerifyNoOtherCalls();
        }
    }
}
