// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cottle;
using Microsoft.DotNet.DockerTools.Infrastructure;
using Microsoft.DotNet.ImageBuilder.Templating;

namespace Microsoft.DotNet.ImageBuilder.Commands;

/// <summary>
/// Writes the <c>eng/docker-tools</c> infrastructure files (pipeline templates, scripts, and docs)
/// that are embedded in this ImageBuilder build to disk. This keeps the pipeline content in a
/// consuming repo coupled to the ImageBuilder version that uses it.
/// </summary>
/// <remarks>
/// The command must be run from the root of a git repository and always targets
/// <c>eng/docker-tools</c> relative to that root. It performs a full mirror: files under the
/// target directory that ImageBuilder no longer ships are removed so the output exactly matches
/// what is embedded.
/// </remarks>
public class UpdateCommand : Command<UpdateOptions>
{
    private static readonly string s_outputRelativePath = PathHelper.SafeCombine("eng", "docker-tools");
    // InfrastructureContent.GetRelativePaths always returns '/'-separated paths, so this is kept in
    // the same form to compare directly without re-normalizing per file.
    private const string DockerImagesRelativePath = "templates/variables/docker-images.yml";
    private const string ImageBuilderTagTemplateVariableName = "IMAGEBUILDER_TAG";

    private static readonly DocumentConfiguration s_templateConfiguration = CottleDocumentConfiguration.Create();

    private readonly IFileSystem _fileSystem;
    private readonly IImageBuilderTagProvider _tagProvider;
    private readonly ILogger<UpdateCommand> _logger;

    public UpdateCommand(IFileSystem fileSystem, IImageBuilderTagProvider tagProvider, ILogger<UpdateCommand> logger)
    {
        _fileSystem = fileSystem;
        _tagProvider = tagProvider;
        _logger = logger;
    }

    protected override string Description => "Writes ImageBuilder's bundled docker-tools infrastructure files to disk";

    public override Task ExecuteAsync()
    {
        string currentDirectory = _fileSystem.GetCurrentDirectory();

        // Ensure we are in a git directory.
        string gitPath = PathHelper.SafeCombine(currentDirectory, ".git");
        if (!_fileSystem.DirectoryExists(gitPath) && !_fileSystem.FileExists(gitPath))
        {
            throw new InvalidOperationException(
                $"{nameof(UpdateCommand)} must be run from the root of a git repository.");
        }

        // Ensure we are running inside a repo that actually uses docker-tools.
        // Require --init to skip this check / onboard a new repo.
        string outputPath = PathHelper.SafeCombine(currentDirectory, s_outputRelativePath);
        if (!Options.Init && !_fileSystem.DirectoryExists(outputPath))
        {
            throw new InvalidOperationException(
                $"The output directory '{outputPath}' does not exist. " +
                $"Pass --init to create it (use this only when onboarding a repo to docker-tools).");
        }

        // Resolve ImageBuilder's image tag.
        // If ImageBuilder doesn't know about its own tag, then use `latest` as a fallback.
        string imageBuilderTag;
        if (_tagProvider.GetTag() is { } resolvedImageBuilderTag && !string.IsNullOrWhiteSpace(resolvedImageBuilderTag))
        {
            imageBuilderTag = resolvedImageBuilderTag;
        }
        else
        {
            _logger.LogWarning(
                "This build of ImageBuilder was not built with the \"IMAGEBUILDER_TAG\" MSBuild property set. " +
                "ImageBuilder tag will fall back to \"latest\".");
            imageBuilderTag = "latest";
        }

        // Clear the existing infrastructure content and re-write it all.
        // This prevents stale files/directories from being left behind.
        if (!Options.IsDryRun)
        {
            try
            {
                _fileSystem.DeleteDirectory(outputPath, recursive: true);
                _logger.LogInformation("Deleted existing directory '{OutputPath}'", outputPath);
            }
            catch (DirectoryNotFoundException)
            {
                // Already absent — the mirror writes everything fresh below, so there's nothing to remove.
            }
        }

        foreach (string relativePath in InfrastructureContent.GetRelativePaths())
        {
            string destinationPath = PathHelper.SafeCombine(outputPath, relativePath);

            if (Options.IsDryRun)
            {
                _logger.LogInformation("[Dry run] Would write '{DestinationPath}'", destinationPath);
                continue;
            }

            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                _fileSystem.CreateDirectory(destinationDirectory);
            }

            _logger.LogInformation("Writing '{DestinationPath}'", destinationPath);

            // docker-images.yml is templated because ImageBuilder needs to self-reference its own image tag.
            if (relativePath == DockerImagesRelativePath)
            {
                string renderedContent = RenderDockerImagesTemplate(relativePath, imageBuilderTag);
                _fileSystem.WriteAllText(destinationPath, renderedContent);
            }
            else
            {
                using Stream source = InfrastructureContent.OpenRead(relativePath);
                using Stream destination = _fileSystem.CreateFile(destinationPath);
                source.CopyTo(destination);
            }
        }

        return Task.CompletedTask;
    }

    private static string RenderDockerImagesTemplate(string relativePath, string imageBuilderTag)
    {
        using Stream source = InfrastructureContent.OpenRead(relativePath);
        using StreamReader reader = new(source);
        string template = reader.ReadToEnd();

        IDocument document = Document.CreateDefault(template, s_templateConfiguration).DocumentOrThrow;
        Dictionary<Value, Value> symbols = new()
        {
            [ImageBuilderTagTemplateVariableName] = imageBuilderTag
        };

        return document.Render(Context.CreateBuiltin(symbols));
    }
}
