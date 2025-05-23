// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.CopyImageHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class CopyBaseImagesCommandTests
    {
        [Fact]
        public async Task MultipleBaseTags()
        {
            const string subscriptionId = "my subscription";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Mock<IEnvironmentService> environmentServiceMock = new();
            Mock<ICopyImageService> copyImageServiceMock = new();
            var copyImageServiceFactoryMock = CreateCopyImageServiceFactoryMock(copyImageServiceMock.Object);

            CopyBaseImagesCommand command = new(
                copyImageServiceFactoryMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IGitService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.Subscription = subscriptionId;
            command.Options.ResourceGroup = "my resource group";
            command.Options.RepoPrefix = "custom-repo/";
            command.Options.CredentialsOptions.Credentials.Add("docker.io", new RegistryCredentials("user", "pass"));
            command.Options.CredentialsOptions.Credentials.Add("my-registry.com", new RegistryCredentials("me", "secret"));

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
                            CreateDockerfile("1.0/runtime/os/amd64", tempFolderContext, "base:tag"),
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
                            new string[] { "amd64" }))),
                CreateRepo("test",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/test/os/amd64", tempFolderContext, "my-registry.com/repo:tag"),
                            new string[] { "amd64" })))
            );
            manifest.Registry = registry;

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
                            subscriptionId,
                            command.Options.ResourceGroup,
                            new string[] { expectedTagInfo.TargetTag },
                            manifest.Registry,
                            expectedTagInfo.SourceImage,
                            expectedTagInfo.Registry,
                            null,
                            It.Is<ContainerRegistryImportSourceCredentials>(creds => creds.Username == expectedTagInfo.Username && creds.Password == expectedTagInfo.Password),
                            false));
            }

            copyImageServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Verifies that we can dynamically override the base image tag that's defined in the Dockerfile with a custom one
        /// that is used for the purposes of copying.
        /// </summary>
        /// <remarks>
        /// In this test, the Dockerfiles contains:
        ///     FROM arm32v7/os:tag
        /// But we want to override that so we don't use that tag as the source of the copy but rather from a custom location.
        /// So it's configured to be overriden to use contoso.azurecr.io/os:tag as the source tag. This ends up getting copied
        /// to mcr.microsoft.com/custom-repo/contoso.azurecr.io/os:tag.
        /// </remarks>
        [Fact]
        public async Task OverridenBaseTag()
        {
            const string subscriptionId = "my subscription";
            const string CustomRegistry = "contoso.azurecr.io";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IEnvironmentService> environmentServiceMock = new();
            Mock<ICopyImageService> copyImageServiceMock = new();
            var copyImageServiceFactoryMock = CreateCopyImageServiceFactoryMock(copyImageServiceMock.Object);

            CopyBaseImagesCommand command = new(
                copyImageServiceFactoryMock.Object, Mock.Of<ILoggerService>(), Mock.Of<IGitService>());
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.Subscription = subscriptionId;
            command.Options.ResourceGroup = "my resource group";
            command.Options.RepoPrefix = "custom-repo/";
            command.Options.CredentialsOptions.Credentials.Add("docker.io", new RegistryCredentials("user", "pass"));
            command.Options.BaseImageOverrideOptions.RegexPattern = @".*\/(os:.*)";
            command.Options.BaseImageOverrideOptions.Substitution = $"{CustomRegistry}/$1";

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile.custom");
            File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

            const string registry = "mcr.microsoft.com";

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/os/arm32v7", tempFolderContext, "arm32v7/os:tag"),
                            new string[] { "os" }),
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/os2/arm32v7", tempFolderContext, "arm32v7/os2:tag"),
                            new string[] { "os2" })))
            );
            manifest.Registry = registry;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            var expectedTagInfos = new(string SourceImage, string TargetTag, string Registry, string Username, string Password)[]
            {
                ( $"os:tag", $"{command.Options.RepoPrefix}{CustomRegistry}/os:tag", CustomRegistry, null, null),
                ( "arm32v7/os2:tag", $"{command.Options.RepoPrefix}arm32v7/os2:tag", "docker.io", "user", "pass" ),
            };

            foreach (var expectedTagInfo in expectedTagInfos)
            {
                copyImageServiceMock.Verify(o =>
                        o.ImportImageAsync(
                            subscriptionId,
                            command.Options.ResourceGroup,
                            new string[] { expectedTagInfo.TargetTag },
                            manifest.Registry,
                            expectedTagInfo.SourceImage,
                            expectedTagInfo.Registry,
                            null,
                            It.Is<ContainerRegistryImportSourceCredentials>(creds => (creds == null && expectedTagInfo.Username == null) || (creds.Username == expectedTagInfo.Username && creds.Password == expectedTagInfo.Password)),
                            false));
            }
        }
    }
}
