// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
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
        List<DockerfileResult> dockerfileResults = await manifest.GetPlatforms()
            .ToAsyncEnumerable()
            .Select((platformEntry, token) => RenderDockerfileAsync(renderer, platformEntry, token))
            .ToListAsync(cancellationToken);

        // Distinct, because a template that fails to load fails the same way for every platform that uses it.
        TemplateError[] templateErrors = [.. dockerfileResults
            .Select(result => result.Value)
            .OfType<TemplateError>()
            .Distinct()];

        if (templateErrors.Length > 0)
        {
            foreach (TemplateError templateError in templateErrors)
            {
                Console.Error.WriteLine($"error: {templateError.Message}");
            }

            return 1;
        }

        RenderedDockerfile[] dockerfiles = [.. dockerfileResults
            .Select(result => result.Value)
            .OfType<RenderedDockerfile>()];

        if (test)
        {
            return await TestDockerfilesAsync(dockerfiles, manifestDirectory, stopwatch, cancellationToken);
        }

        foreach (RenderedDockerfile dockerfile in dockerfiles)
        {
            string dockerfileFullPath = Path.Join(manifestDirectory, dockerfile.DockerfilePath);
            FileInfo dockerfileFile = new(dockerfileFullPath);
            dockerfileFile.Directory?.Create();
            await File.WriteAllTextAsync(dockerfileFile.FullName, dockerfile.Content, cancellationToken);
        }

        Console.WriteLine($"Generated {dockerfiles.Length} files in {stopwatch.ElapsedMilliseconds}ms");
        return 0;
    }

    private static async ValueTask<DockerfileResult> RenderDockerfileAsync(
        TemplateRenderer renderer,
        PlatformEntry platformEntry,
        CancellationToken cancellationToken)
    {
        if (platformEntry.Platform is not { DockerfileTemplate: string templatePath, Dockerfile: string dockerfilePath })
        {
            return new NoTemplate();
        }

        Dictionary<string, JsonElement> variables = platformEntry.GetVariables();
        RenderResult renderResult = await renderer.RenderAsync(templatePath, variables, cancellationToken);
        return renderResult switch
        {
            RenderedTemplate renderedTemplate => new RenderedDockerfile(dockerfilePath, renderedTemplate.Content),
            TemplateLoadError error => new TemplateError($"{error.TemplatePath}: {error.Message}"),
            TemplateRenderError error => new TemplateError($"{error.TemplatePath} ({platformEntry.Key}): {error.Message}"),
        };
    }

    private static async Task<int> TestDockerfilesAsync(
        RenderedDockerfile[] dockerfiles,
        string manifestDirectory,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        List<DockerfileCheck> dockerfileChecks = await dockerfiles
            .ToAsyncEnumerable()
            .Select((dockerfile, token) => CheckDockerfileAsync(dockerfile, manifestDirectory, token))
            .ToListAsync(cancellationToken);

        string[] mismatches = [.. dockerfileChecks
            .Select(DescribeMismatch)
            .OfType<string>()];

        foreach (string mismatch in mismatches)
        {
            Console.Error.WriteLine($"error: {mismatch}");
        }

        Console.WriteLine($"Checked {dockerfiles.Length} files in {stopwatch.ElapsedMilliseconds}ms");
        return mismatches.Length > 0 ? 1 : 0;
    }

    private static async ValueTask<DockerfileCheck> CheckDockerfileAsync(
        RenderedDockerfile dockerfile,
        string manifestDirectory,
        CancellationToken cancellationToken)
    {
        string dockerfileFullPath = Path.Join(manifestDirectory, dockerfile.DockerfilePath);
        if (!File.Exists(dockerfileFullPath))
        {
            return new MissingDockerfile(dockerfile.DockerfilePath);
        }

        string existingContent = await File.ReadAllTextAsync(dockerfileFullPath, cancellationToken);

        // Git may check out Dockerfiles with CRLF line endings, so only compare the text.
        string normalizedContent = existingContent.ReplaceLineEndings("\n");
        return normalizedContent == dockerfile.Content
            ? new MatchingDockerfile(dockerfile.DockerfilePath)
            : new MismatchedDockerfile(dockerfile.DockerfilePath);
    }

    private static string? DescribeMismatch(DockerfileCheck dockerfileCheck) => dockerfileCheck switch
    {
        MatchingDockerfile => null,
        MismatchedDockerfile mismatched => $"{mismatched.DockerfilePath} doesn't match its template",
        MissingDockerfile missing => $"{missing.DockerfilePath} doesn't exist",
    };

    private sealed record RenderedDockerfile(string DockerfilePath, string Content);

    private sealed record NoTemplate;

    private sealed record TemplateError(string Message);

    private readonly union DockerfileResult(RenderedDockerfile, NoTemplate, TemplateError);

    private sealed record MatchingDockerfile(string DockerfilePath);

    private sealed record MismatchedDockerfile(string DockerfilePath);

    private sealed record MissingDockerfile(string DockerfilePath);

    private readonly union DockerfileCheck(MatchingDockerfile, MismatchedDockerfile, MissingDockerfile);
}
