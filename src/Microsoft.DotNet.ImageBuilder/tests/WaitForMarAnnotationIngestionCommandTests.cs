// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class WaitForMarAnnotationIngestionCommandTests
{
    [Fact]
    public async Task WaitForMarAnnotationIngestionCommand()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        EolAnnotationsData eolAnnotations = new()
        {
            EolDigests =
                [
                    new EolDigestData { Digest = "registry.azurecr.io/repo@sha256:digest1" },
                    new EolDigestData { Digest = "registry.azurecr.io/repo2@sha256:digest2", }
                ]
        };

        string eolDigestsListPath = Path.Combine(tempFolderContext.Path, "eol-digests.json");
        File.WriteAllText(eolDigestsListPath, JsonConvert.SerializeObject(eolAnnotations));

        Mock<IMarImageIngestionReporter> ingestionReporter = new();
        WaitForMarAnnotationIngestionCommand cmd = new(
            Mock.Of<ILoggerService>(),
            ingestionReporter.Object);
        cmd.Options.EolDigestsListPath = eolDigestsListPath;

        await cmd.ExecuteAsync();

        DigestInfo[] expectedDigests =
            [
                new DigestInfo("sha256:digest1", "repo", tags: []),
                new DigestInfo("sha256:digest2", "repo2", tags: [])
            ];

        ingestionReporter.Verify(
            r => r.ReportImageStatusesAsync(
                It.Is<IEnumerable<DigestInfo>>(actualDigests => actualDigests.SequenceEqual(expectedDigests, DigestInfoEqualityComparer.Instance)),
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<DateTime?>()));
    }
}
