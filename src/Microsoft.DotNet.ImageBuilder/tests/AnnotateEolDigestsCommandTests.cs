﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Models.Oras;
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
        private const string RepoPrefix = "public/";
        private const string AcrName = "myacr.azurecr.io";
        private const string AnnotationsOutputPath = "annotations.txt";
        private const string AnnotationDigest1 = "annotationdigest1";
        private const string AnnotationDigest2 = "annotationdigest2";

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

            OciManifest manifest;
            orasServiceMock.Verify(
                o => o.AnnotateEolDigest("digest1", _globalDate, It.IsAny<ILoggerService>(), It.IsAny<bool>(), out manifest));
            orasServiceMock.Verify(
                o => o.AnnotateEolDigest("digest2", _specificDigestDate, It.IsAny<ILoggerService>(), It.IsAny<bool>(), out manifest));

            string[] expectedAnnotationDigests =
                [
                    $"{AcrName}/{RepoPrefix}@{AnnotationDigest1}",
                    $"{AcrName}/{RepoPrefix}@{AnnotationDigest2}"
                ];
            string[] annotationDigests = File.ReadAllLines(Path.Combine(tempFolderContext.Path, AnnotationsOutputPath));
            Assert.Equal(expectedAnnotationDigests, annotationDigests);
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

            OciManifest manifest;
            orasServiceMock.Verify(
                o => o.AnnotateEolDigest(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<ILoggerService>(), It.IsAny<bool>(), out manifest),
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

            OciManifest manifest;
            orasServiceMock.Verify(
                o => o.AnnotateEolDigest(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<ILoggerService>(), It.IsAny<bool>(), out manifest),
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
            AnnotateEolDigestsCommand command = new(
                loggerServiceMock.Object,
                orasServiceMock.Object,
                Mock.Of<IRegistryCredentialsProvider>());
            command.Options.RepoPrefix = RepoPrefix;
            command.Options.AcrName = AcrName;
            command.Options.EolDigestsListPath = eolDigestsListPath;
            command.Options.AnnotationDigestsOutputPath = Path.Combine(tempFolderContext.Path, AnnotationsOutputPath);
            return command;
        }

        private Mock<IOrasService> CreateOrasServiceMock(bool digestAlreadyAnnotated, bool digestAnnotationIsSuccessful, bool useNonMatchingDate)
        {
            Mock<IOrasService> orasServiceMock = new();
            SetupIsDigestAnnotatedForEolMethod(orasServiceMock, "digest1", digestAlreadyAnnotated, _globalDate, useNonMatchingDate);
            SetupIsDigestAnnotatedForEolMethod(orasServiceMock, "digest2", digestAlreadyAnnotated, _specificDigestDate, useNonMatchingDate);

            OciManifest digest1Annotation = new()
            {
                Reference = $"{AcrName}/{RepoPrefix}@{AnnotationDigest1}"
            };

            orasServiceMock
                .Setup(o => o.AnnotateEolDigest(It.Is<string>(digest => digest.Contains("digest1")), It.IsAny<DateOnly>(), It.IsAny<ILoggerService>(), It.IsAny<bool>(), out digest1Annotation))
                .Returns(digestAnnotationIsSuccessful);

            OciManifest digest2Annotation = new()
            {
                Reference = $"{AcrName}/{RepoPrefix}@{AnnotationDigest2}"
            };

            orasServiceMock
                .Setup(o => o.AnnotateEolDigest(It.Is<string>(digest => digest.Contains("digest2")), It.IsAny<DateOnly>(), It.IsAny<ILoggerService>(), It.IsAny<bool>(), out digest2Annotation))
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
                    },
                    Reference = $"{AcrName}/{RepoPrefix}repo@{digest}"
                };
            }

            orasServiceMock
                .Setup(o => o.IsDigestAnnotatedForEol(digest, It.IsAny<ILoggerService>(), It.IsAny<bool>(), out manifest))
                .Returns(digestAlreadyAnnotated);
        }
    }
}
