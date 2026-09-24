// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class ArtifactServiceTests
{
    private static readonly string s_outputRoot = Path.Combine(Path.GetTempPath(), "artifacts");

    [TestMethod]
    public void ResolvePath_RelativePath_ResolvesUnderArtifactStagingDirectory()
    {
        var service = CreateService(new InMemoryFileSystem());

        string path = service.ResolvePath(Path.Combine("image-info", "input.json"));

        path.ShouldBe(Path.Combine(s_outputRoot, "image-info", "input.json"));
    }

    [TestMethod]
    public void WriteAllText_WritesUnderArtifactStagingDirectory()
    {
        var fileSystem = new InMemoryFileSystem();
        var service = CreateService(fileSystem);
        string expectedPath = Path.Combine(s_outputRoot, "image-info", "output.json");

        service.WriteAllText(Path.Combine("image-info", "output.json"), "contents");

        fileSystem.DirectoriesCreated.ShouldContain(Path.GetDirectoryName(expectedPath));
        fileSystem.GetFileText(expectedPath).ShouldBe("contents");
    }

    [TestMethod]
    public void WriteAllText_MissingArtifactStagingDirectory_Throws()
    {
        var service = new ArtifactService(
            new InMemoryFileSystem(),
            Options.Create(new BuildConfiguration()));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => service.WriteAllText("output.json", "contents"));

        exception.Message.ShouldContain(nameof(BuildConfiguration.ArtifactStagingDirectory));
    }

    [TestMethod]
    public void WriteAllText_PathOutsideArtifactStagingDirectory_Throws()
    {
        var service = CreateService(new InMemoryFileSystem());

        Should.Throw<ArgumentException>(
            () => service.WriteAllText(Path.Combine("..", "output.json"), "contents"));
    }

    private static ArtifactService CreateService(IFileSystem fileSystem) =>
        new(
            fileSystem,
            Options.Create(
                new BuildConfiguration
                {
                    ArtifactStagingDirectory = s_outputRoot
                }));
}
