// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
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

            Mock<IOrasService> orasServiceMock;

            AnnotateEolDigestsCommand command =
                InitializeCommand(
                    tempFolderContext,
                    out orasServiceMock,
                    digestAlreadyAnnotated: false,
                    digestAnnotationIsSuccessful: true);
            await command.ExecuteAsync();

            orasServiceMock.Verify(
                o => o.AnnotateEolDigest("digest1", _globalDate, It.IsAny<ILoggerService>(), It.IsAny<bool>()));
            orasServiceMock.Verify(
                o => o.AnnotateEolDigest("digest2", _specificDigestDate, It.IsAny<ILoggerService>(), It.IsAny<bool>()));
        }

        [Fact]
        public async Task AnnotateEolDigestsCommand_AnnotationFailures()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IOrasService> orasServiceMock;

            AnnotateEolDigestsCommand command =
                InitializeCommand(
                    tempFolderContext,
                    out orasServiceMock,
                    digestAlreadyAnnotated: false,
                    digestAnnotationIsSuccessful: false);

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());
            Assert.Contains(
                $"(failed: 2, skipped: 0)",
                ex.Message);
        }

        [Fact]
        public async Task AnnotateEolDigestsCommand_CheckAnnotations_AlreadyAnnotated_NonMatchingEolDate()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IOrasService> orasServiceMock;

            AnnotateEolDigestsCommand command =
                InitializeCommand(
                    tempFolderContext,
                    out orasServiceMock,
                    digestAlreadyAnnotated: true,
                    digestAnnotationIsSuccessful: true,
                    useNonMatchingDate: true);

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());
            Assert.Contains(
                $"(failed: 0, skipped: 2)",
                ex.Message);
            orasServiceMock.Verify(
                o => o.AnnotateEolDigest(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<ILoggerService>(), It.IsAny<bool>()),
                Times.Never());
        }

        [Fact]
        public async Task AnnotateEolDigestsCommand_CheckAnnotations_AlreadyAnnotated_MatchingEolDate()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<IOrasService> orasServiceMock;

            AnnotateEolDigestsCommand command =
                InitializeCommand(
                    tempFolderContext,
                    out orasServiceMock,
                    digestAlreadyAnnotated: true,
                    digestAnnotationIsSuccessful: true);

            await command.ExecuteAsync();
            orasServiceMock.Verify(
                o => o.AnnotateEolDigest(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<ILoggerService>(), It.IsAny<bool>()),
                Times.Never());
        }

        private AnnotateEolDigestsCommand InitializeCommand(
            TempFolderContext tempFolderContext,
            out Mock<IOrasService> orasServiceMock,
            bool digestAlreadyAnnotated = true,
            bool digestAnnotationIsSuccessful = true,
            bool useNonMatchingDate = false)
        {
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

            Mock<ILoggerService> loggerServiceMock = new();
            orasServiceMock = CreateOrasServiceMock(digestAlreadyAnnotated, digestAnnotationIsSuccessful, useNonMatchingDate);
            Mock<IRegistryCredentialsProvider> registryCredentialsProviderMock = CreateRegistryCredentialsProviderMock();
            AnnotateEolDigestsCommand command = new(
                loggerServiceMock.Object,
                orasServiceMock.Object,
                registryCredentialsProviderMock.Object);
            command.Options.EolDigestsListOutputPath = eolDigestsListPath;
            command.Options.CredentialsOptions.Credentials.Add("mcr.microsoft.com", new RegistryCredentials("user", "pass"));
            return command;
        }

        private static Mock<IRegistryCredentialsProvider> CreateRegistryCredentialsProviderMock()
        {
            Mock<IRegistryCredentialsProvider> registryCredentialsProviderMock = new();
            registryCredentialsProviderMock
                .Setup(o => o.GetCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RegistryCredentialsOptions>()))
                .ReturnsAsync(new RegistryCredentials("username", "password"));

            return registryCredentialsProviderMock;
        }

        private Mock<IOrasService> CreateOrasServiceMock(bool digestAlreadyAnnotated, bool digestAnnotationIsSuccessful, bool useNonMatchingDate)
        {
            Mock<IOrasService> orasServiceMock = new();
            SetupIsDigestAnnotatedForEolMethod(orasServiceMock, "digest1", digestAlreadyAnnotated, _globalDate, useNonMatchingDate);
            SetupIsDigestAnnotatedForEolMethod(orasServiceMock, "digest2", digestAlreadyAnnotated, _specificDigestDate, useNonMatchingDate);

            orasServiceMock
                .Setup(o => o.AnnotateEolDigest(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<ILoggerService>(), It.IsAny<bool>()))
                .Returns(digestAnnotationIsSuccessful);

            return orasServiceMock;
        }

        private static void SetupIsDigestAnnotatedForEolMethod(Mock<IOrasService> orasServiceMock, string digest, bool digestAlreadyAnnotated, DateOnly eolDate, bool useNonMatchingDate)
        {
            if (useNonMatchingDate)
            {
                eolDate = eolDate.AddDays(1);
            }

            OciManifest manifest = null;
            if (digestAlreadyAnnotated)
            {
                manifest = new OciManifest
                {
                    Annotations = new Dictionary<string, string>
                    {
                        { OrasService.EndOfLifeAnnotation, eolDate.ToString("yyyy-MM-dd") }
                    }
                };
            }

            orasServiceMock
                .Setup(o => o.IsDigestAnnotatedForEol(digest, It.IsAny<ILoggerService>(), It.IsAny<bool>(), out manifest))
                .Returns(digestAlreadyAnnotated);
        }
    }
}
