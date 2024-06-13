// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.EolAnnotations;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class AnnotateEolDigestsCommandTests
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DateOnly _globalDate = new DateOnly(2024, 6, 10);
        private readonly DateOnly _specificDigestDate = new DateOnly(2022, 1, 1);

        public AnnotateEolDigestsCommandTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task AnnotateEolDigestsCommand_AnnotationSuccess()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<ILoggerService> loggerServiceMock;

            AnnotateEolDigestsCommand command =
                InitializeCommand(
                    tempFolderContext,
                    out loggerServiceMock,
                    noCheck: true,
                    digestAlreadyAnnotated: true,
                    digestAnnotationIsSuccessful: true);
            command.LoadManifest();
            await command.ExecuteAsync();

            loggerServiceMock.Verify(o => o.WriteMessage($"Annotating EOL for digest 'digest1', date '{_globalDate}'"));
            loggerServiceMock.Verify(o => o.WriteMessage($"Annotating EOL for digest 'digest2', date '{_specificDigestDate}'"));
        }

        [Fact]
        public async Task AnnotateEolDigestsCommand_AnnotationFailures()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<ILoggerService> loggerServiceMock;

            AnnotateEolDigestsCommand command =
                InitializeCommand(
                    tempFolderContext,
                    out loggerServiceMock,
                    noCheck: true,
                    digestAlreadyAnnotated: true,
                    digestAnnotationIsSuccessful: false);
            command.LoadManifest();

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());
            Assert.StartsWith(
                $"Failed to annotate 2 digests for EOL.",
                ex.Message);
        }

        [Fact]
        public async Task AnnotateEolDigestsCommand_CheckAnnotations_AlreadyAnnotated()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<ILoggerService> loggerServiceMock;

            AnnotateEolDigestsCommand command =
                InitializeCommand(
                    tempFolderContext,
                    out loggerServiceMock,
                    noCheck: false,
                    digestAlreadyAnnotated: true,
                    digestAnnotationIsSuccessful: true);
            command.LoadManifest();
            await command.ExecuteAsync();

            loggerServiceMock.Verify(o => o.WriteMessage("Digest 'digest1' is already annotated for EOL."));
            loggerServiceMock.Verify(o => o.WriteMessage("Digest 'digest2' is already annotated for EOL."));
        }

        private AnnotateEolDigestsCommand InitializeCommand(
            TempFolderContext tempFolderContext,
            out Mock<ILoggerService> loggerServiceMock,
            bool noCheck = true,
            bool digestAlreadyAnnotated = true,
            bool digestAnnotationIsSuccessful = true)
        {
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

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            EolAnnotationsData eolAnnotations = new()
            {
                EolDate = _globalDate,
                EolDigests = new List<EolDigestData>
                {
                    new EolDigestData { Digest = "digest1" },
                    new EolDigestData { Digest = "digest2", EolDate = _specificDigestDate }
                }
            };

            string eolDigestsListPath = Path.Combine(tempFolderContext.Path, "eol-digests.json");
            File.WriteAllText(eolDigestsListPath, JsonConvert.SerializeObject(eolAnnotations));

            loggerServiceMock = new();
            Mock<IOrasService> orasServiceMock = CreateOrasServiceMock(digestAlreadyAnnotated, digestAnnotationIsSuccessful);
            Mock<IRegistryCredentialsProvider> registryCredentialsProviderMock = CreateRegistryCredentialsProviderMock();
            AnnotateEolDigestsCommand command = new(
                Mock.Of<IDockerService>(),
                loggerServiceMock.Object,
                Mock.Of<IProcessService>(),
                orasServiceMock.Object,
                registryCredentialsProviderMock.Object);
            command.Options.EolDigestsListPath = eolDigestsListPath;
            command.Options.Subscription = "941d4baa-5ef2-462e-b4b1-505791294610";
            command.Options.ResourceGroup = "DotnetContainers";
            command.Options.NoCheck = noCheck;
            command.Options.CredentialsOptions.Credentials.Add("mcr.microsoft.com", new RegistryCredentials("user", "pass"));
            command.Options.Manifest = manifestPath;
            return command;
        }

        private Mock<IRegistryCredentialsProvider> CreateRegistryCredentialsProviderMock()
        {
            Mock<IRegistryCredentialsProvider> registryCredentialsProviderMock = new();
            registryCredentialsProviderMock
                .Setup(o => o.GetCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RegistryCredentialsOptions>()))
                .ReturnsAsync(new RegistryCredentials("username", "password"));

            return registryCredentialsProviderMock;
        }

        private Mock<IOrasService> CreateOrasServiceMock(bool digestAlreadyAnnotated = true, bool digestAnnotationIsSuccessful = true)
        {
            Mock<IOrasService> orasServiceMock = new();
            orasServiceMock
                .Setup(o => o.IsDigestAnnotatedForEol(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(digestAlreadyAnnotated);

            orasServiceMock
                .Setup(o => o.AnnotateEolDigest(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<ILoggerService>(), It.IsAny<bool>()))
                .Returns(digestAnnotationIsSuccessful);

            return orasServiceMock;
        }
    }
}
