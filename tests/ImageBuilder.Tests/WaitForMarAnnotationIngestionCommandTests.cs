// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.ImageBuilder;
using Microsoft.DotNet.DockerTools.ImageBuilder.Commands;
using Microsoft.DotNet.DockerTools.ImageBuilder.Tests.Helpers;
using Moq;
using Xunit;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Tests;

public class WaitForMarAnnotationIngestionCommandTests
{
    [Fact]
    public async Task WaitForMarAnnotationIngestionCommand()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string annotationsDigestsPath = Path.Combine(tempFolderContext.Path, "annotations.txt");
        File.WriteAllLines(annotationsDigestsPath,
            [
                "registry.azurecr.io/repo@sha256:digest1",
                "registry.azurecr.io/repo2@sha256:digest2"
            ]);

        Mock<IMarImageIngestionReporter> ingestionReporter = new();
        WaitForMarAnnotationIngestionCommand cmd = new(
            Mock.Of<ILoggerService>(),
            ingestionReporter.Object);
        cmd.Options.AnnotationDigestsPath = annotationsDigestsPath;

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
