#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ConfigurationHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class CopyBaseImagesCommandTests
    {
        private const string SubscriptionId = "my subscription";
        private const string ResourceGroup = "my resource group";
        private const string DestinationRegistry = "mcr.microsoft.com";

        [Fact]
        public async Task MultipleBaseTags()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<ICopyImageService> copyImageServiceMock = new();

            CopyBaseImagesCommand command = new(
                TestHelper.CreateManifestJsonService(),
                copyImageServiceMock.Object,
                Mock.Of<ILogger<CopyBaseImagesCommand>>(),
                Mock.Of<IGitService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.RepoPrefix = "custom-repo/";
            command.Options.CredentialsOptions.Credentials.Add("docker.io", new RegistryCredentials("user", "pass"));
            command.Options.CredentialsOptions.Credentials.Add("my-registry.com", new RegistryCredentials("me", "secret"));

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile.custom");
            File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/os/arm32v7", tempFolderContext, "arm32v7/base:tag"),
                            new string[] { "arm32v7" }),
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/os/amd64", tempFolderContext, "base:tag"),
                            new string[] { "amd64" }))),
                CreateRepo("aspnet",
                    CreateImage(
                        CreatePlatformWithRepoBuildArg(
                            CreateDockerfile("1.0/aspnet/os/arm32v7", tempFolderContext, "$REPO:arm32v7"),
                            $"{DestinationRegistry}/{command.Options.RepoPrefix}aspnet",
                            new string[] { "arm32v7" }),
                        CreatePlatformWithRepoBuildArg(
                            CreateDockerfile("1.0/aspnet/os/amd64", tempFolderContext, "$REPO:amd64"),
                            $"{DestinationRegistry}/{command.Options.RepoPrefix}aspnet",
                            new string[] { "amd64" }))),
                CreateRepo("test",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/test/os/amd64", tempFolderContext, "my-registry.com/repo:tag"),
                            new string[] { "amd64" })))
            );
            manifest.Registry = DestinationRegistry;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            var expectedTagInfos = new (string SourceImage, string TargetTag, string Registry, string Username, string Password)[]
            {
                ( "arm32v7/base:tag", $"{command.Options.RepoPrefix}arm32v7/base:tag", "docker.io", "user", "pass" ),
                ( "library/base:tag", $"{command.Options.RepoPrefix}library/base:tag", "docker.io", "user", "pass" ),
                ( "repo:tag", $"{command.Options.RepoPrefix}my-registry.com/repo:tag", "my-registry.com", "me", "secret" )
            };

            foreach (var expectedTagInfo in expectedTagInfos)
            {
                copyImageServiceMock.Verify(o =>
                        o.ImportImageAsync(
                            new string[] { expectedTagInfo.TargetTag },
                            manifest.Registry,
                            expectedTagInfo.SourceImage,
                            false,
                            expectedTagInfo.Registry,
                            It.Is<ContainerRegistryImportSourceCredentials>(creds => creds.Username == expectedTagInfo.Username && creds.Password == expectedTagInfo.Password),
                            false));
            }

            copyImageServiceMock.VerifyNoOtherCalls();
        }
    }
}
