// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Fluid;
using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

internal static class GenerateCommand
{
    public static Command Create()
    {
        Argument<FileInfo> manifestArgument = ManifestArgument.Create();
        Option<bool> testOption = new("--test")
        {
            Description = "Checks that each Dockerfile matches its template instead of writing it."
        };

        Command command = new("generate", "Generates each platform's Dockerfile from its template.")
        {
            manifestArgument,
            testOption
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            FileInfo manifestFile = parseResult.GetRequiredValue(manifestArgument);
            bool test = parseResult.GetValue(testOption);
            return GenerateAsync(manifestFile, test, cancellationToken);
        });

        return command;
    }

    private static async Task<int> GenerateAsync(FileInfo manifestFile, bool test, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Manifest manifest = await ManifestLoader.LoadAsync(manifestFile.FullName, cancellationToken);
        string manifestDirectory = manifestFile.DirectoryName
            ?? throw new ArgumentException($"'{manifestFile}' is not a file path.");

        TemplateRenderer renderer = new(manifestDirectory);
        List<RenderedDockerfile> dockerfiles = [];
        List<string> errors = [];

        foreach (PlatformEntry platformEntry in manifest.GetPlatforms())
        {
            string? templatePath = platformEntry.Platform.DockerfileTemplate;
            string? dockerfilePath = platformEntry.Platform.Dockerfile;
            if (templatePath is null || dockerfilePath is null)
            {
                continue;
            }

            Dictionary<string, JsonElement> variables = platformEntry.GetVariables();
            try
            {
                string dockerfileContent = await renderer.RenderAsync(templatePath, variables, cancellationToken);
                dockerfiles.Add(new RenderedDockerfile(dockerfilePath, dockerfileContent));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                // Reading or parsing a template fails the same way for every platform that uses it.
                if (!errors.Contains(exception.Message))
                {
                    errors.Add(exception.Message);
                }
            }
            catch (FluidException exception)
            {
                errors.Add($"{templatePath} ({platformEntry.Key}): {exception.Message}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Console.Error.WriteLine($"error: {error}");
            }

            return 1;
        }

        if (test)
        {
            int mismatchCount = await TestDockerfilesAsync(dockerfiles, manifestDirectory, cancellationToken);
            Console.WriteLine($"Checked {dockerfiles.Count} files in {stopwatch.ElapsedMilliseconds}ms");
            return mismatchCount > 0 ? 1 : 0;
        }

        await WriteDockerfilesAsync(dockerfiles, manifestDirectory, cancellationToken);
        Console.WriteLine($"Generated {dockerfiles.Count} files in {stopwatch.ElapsedMilliseconds}ms");
        return 0;
    }

    private static async Task WriteDockerfilesAsync(
        List<RenderedDockerfile> dockerfiles,
        string manifestDirectory,
        CancellationToken cancellationToken)
    {
        foreach (RenderedDockerfile dockerfile in dockerfiles)
        {
            string dockerfileFullPath = Path.Join(manifestDirectory, dockerfile.DockerfilePath);
            FileInfo dockerfileFile = new(dockerfileFullPath);
            dockerfileFile.Directory?.Create();
            await File.WriteAllTextAsync(dockerfileFile.FullName, dockerfile.Content, cancellationToken);
        }
    }

    /// <summary>
    /// Reports each Dockerfile that doesn't match its rendered template.
    /// </summary>
    /// <returns>The number of Dockerfiles that don't match.</returns>
    private static async Task<int> TestDockerfilesAsync(
        List<RenderedDockerfile> dockerfiles,
        string manifestDirectory,
        CancellationToken cancellationToken)
    {
        int mismatchCount = 0;
        foreach (RenderedDockerfile dockerfile in dockerfiles)
        {
            string dockerfileFullPath = Path.Join(manifestDirectory, dockerfile.DockerfilePath);
            if (!File.Exists(dockerfileFullPath))
            {
                Console.Error.WriteLine($"error: {dockerfile.DockerfilePath} doesn't exist");
                mismatchCount++;
                continue;
            }

            string existingContent = await File.ReadAllTextAsync(dockerfileFullPath, cancellationToken);

            // Git may check out Dockerfiles with CRLF line endings, so only compare the text.
            string normalizedContent = existingContent.ReplaceLineEndings("\n");
            if (normalizedContent != dockerfile.Content)
            {
                Console.Error.WriteLine($"error: {dockerfile.DockerfilePath} doesn't match its template");
                mismatchCount++;
            }
        }

        return mismatchCount;
    }

    private sealed record RenderedDockerfile(string DockerfilePath, string Content);
}