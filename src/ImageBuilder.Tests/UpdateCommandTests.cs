// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.Infrastructure;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class UpdateCommandTests
{
    // Use the platform's directory separator for the fake root so that paths derived via
    // Path.Combine and Path.GetDirectoryName stay consistent (Path.GetDirectoryName normalizes
    // a leading '/' to '\' on Windows, which would otherwise not match the in-memory entries).
    private static readonly string s_repoRoot = $"{Path.DirectorySeparatorChar}repo";
    private static readonly string s_outputPath = PathHelper.SafeCombine(s_repoRoot, "eng", "docker-tools");

    [TestMethod]
    public async Task UpdateCommand_WritesAllEmbeddedFiles()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(s_outputPath);
        UpdateCommand command = CreateCommand(fileSystem);

        await command.ExecuteAsync();

        IReadOnlyList<string> expectedPaths = InfrastructureContent.GetRelativePaths();
        expectedPaths.ShouldNotBeEmpty();
        List<string> renderedDestinations = [];

        foreach (string relativePath in expectedPaths)
        {
            string expectedDestination = PathHelper.SafeCombine(s_outputPath, relativePath);
            fileSystem.FileExists(expectedDestination).ShouldBeTrue();

            using Stream expectedStream = InfrastructureContent.OpenRead(relativePath);
            using MemoryStream expectedBytes = new();
            expectedStream.CopyTo(expectedBytes);
            if (fileSystem.GetFileBytes(expectedDestination).SequenceEqual(expectedBytes.ToArray()))
            {
                continue;
            }

            renderedDestinations.Add(expectedDestination);
        }

        renderedDestinations.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task UpdateCommand_ImageBuilderTagAssemblyMetadataSet_RendersDockerImagesTemplate()
    {
        const string tag = "1234567";
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(s_outputPath);
        UpdateCommand command = CreateCommand(fileSystem, tag);

        await command.ExecuteAsync();

        string content = GetRenderedImageBuilderVariables(fileSystem, $"image-builder:{tag}");
        content.ShouldContain($"image-builder:{tag}");
        content.ShouldNotContain("{{");
    }

    [TestMethod]
    public async Task UpdateCommand_ImageBuilderTagAssemblyMetadataMissing_FallsBackToLatestWithWarning()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(s_outputPath);
        Mock<ILogger<UpdateCommand>> logger = new();
        UpdateCommand command = CreateCommand(fileSystem, tag: null, logger: logger);

        await command.ExecuteAsync();

        string content = GetRenderedImageBuilderVariables(fileSystem, "image-builder:latest");
        content.ShouldContain("image-builder:latest");
        content.ShouldNotContain("{{");

        logger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task UpdateCommand_DeletesStaleFiles()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(s_outputPath);
        string staleFile = PathHelper.SafeCombine(s_outputPath, "templates", "removed-template.yml");
        fileSystem.AddFile(staleFile, "stale");
        UpdateCommand command = CreateCommand(fileSystem);

        await command.ExecuteAsync();

        fileSystem.FileExists(staleFile).ShouldBeFalse();
        fileSystem.FilesDeleted.ShouldContain(staleFile);
    }

    [TestMethod]
    public async Task UpdateCommand_DeletesStaleDirectories()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(s_outputPath);
        string staleDirectory = PathHelper.SafeCombine(s_outputPath, "obsolete");
        string staleFile = PathHelper.SafeCombine(staleDirectory, "old.yml");
        fileSystem.AddFile(staleFile, "stale");
        UpdateCommand command = CreateCommand(fileSystem);

        await command.ExecuteAsync();

        fileSystem.DirectoryExists(staleDirectory).ShouldBeFalse();
        fileSystem.DirectoriesDeleted.ShouldContain(staleDirectory);
    }

    [TestMethod]
    public async Task UpdateCommand_NotGitRoot_Throws()
    {
        InMemoryFileSystem fileSystem = new() { CurrentDirectory = s_repoRoot };
        fileSystem.AddDirectory(s_outputPath);
        UpdateCommand command = CreateCommand(fileSystem);

        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(() => command.ExecuteAsync());
        exception.Message.ShouldContain("root of a git repository");
    }

    [TestMethod]
    public async Task UpdateCommand_OutputMissingWithoutInit_Throws()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        UpdateCommand command = CreateCommand(fileSystem);

        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(() => command.ExecuteAsync());
        exception.Message.ShouldContain("--init");
        fileSystem.FilesWritten.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task UpdateCommand_OutputMissingWithInit_CreatesAndWrites()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        UpdateCommand command = CreateCommand(fileSystem);
        command.Options.Init = true;

        await command.ExecuteAsync();

        fileSystem.DirectoriesCreated.ShouldContain(s_outputPath);
        fileSystem.FilesWritten.ShouldNotBeEmpty();
    }

    [TestMethod]
    public async Task UpdateCommand_DryRun_MakesNoChanges()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(s_outputPath);
        string staleFile = PathHelper.SafeCombine(s_outputPath, "templates", "removed-template.yml");
        fileSystem.AddFile(staleFile, "stale");
        UpdateCommand command = CreateCommand(fileSystem);
        command.Options.IsDryRun = true;

        await command.ExecuteAsync();

        fileSystem.FilesWritten.ShouldBeEmpty();
        fileSystem.FilesDeleted.ShouldBeEmpty();
        fileSystem.DirectoriesDeleted.ShouldBeEmpty();
        fileSystem.FileExists(staleFile).ShouldBeTrue();
    }

    private static InMemoryFileSystem CreateRepoFileSystem()
    {
        InMemoryFileSystem fileSystem = new() { CurrentDirectory = s_repoRoot };
        fileSystem.AddDirectory(PathHelper.SafeCombine(s_repoRoot, ".git"));
        return fileSystem;
    }

    private static UpdateCommand CreateCommand(
        IFileSystem fileSystem,
        string? tag = null,
        Mock<ILogger<UpdateCommand>>? logger = null)
    {
        Mock<IImageBuilderTagProvider> tagProvider = new();
        tagProvider.Setup(provider => provider.GetTag()).Returns(tag);
        return new UpdateCommand(
            fileSystem,
            tagProvider.Object,
            (logger ?? new Mock<ILogger<UpdateCommand>>()).Object);
    }

    private static string GetRenderedImageBuilderVariables(InMemoryFileSystem fileSystem, string expectedImageBuilderReference) =>
        fileSystem.FilesWritten
            .Select(path => Encoding.UTF8.GetString(fileSystem.GetFileBytes(path)))
            .Single(content => content.Contains(expectedImageBuilderReference));
}
