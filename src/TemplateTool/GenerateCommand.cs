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

        command.SetAction(parseResult =>
        {
            FileInfo manifestFile = parseResult.GetRequiredValue(manifestArgument);
            bool test = parseResult.GetValue(testOption);
            return Generate(manifestFile, test);
        });

        return command;
    }

    private static int Generate(FileInfo manifestFile, bool test)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Manifest manifest = ManifestLoader.Load(manifestFile.FullName);
        string manifestDirectory = manifestFile.DirectoryName
            ?? throw new ArgumentException($"'{manifestFile}' is not a file path.");

        TemplateRenderer renderer = new(manifestDirectory);
        DockerfileResult[] dockerfileResults = [.. manifest.GetPlatforms()
            .Select(platformEntry => RenderDockerfile(renderer, platformEntry))];

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
            return TestDockerfiles(dockerfiles, manifestDirectory, stopwatch);
        }

        foreach (RenderedDockerfile dockerfile in dockerfiles)
        {
            string dockerfileFullPath = Path.Join(manifestDirectory, dockerfile.DockerfilePath);
            FileInfo dockerfileFile = new(dockerfileFullPath);
            dockerfileFile.Directory?.Create();
            File.WriteAllText(dockerfileFile.FullName, dockerfile.Content);
        }

        Console.WriteLine($"Generated {dockerfiles.Length} files in {stopwatch.ElapsedMilliseconds}ms");
        return 0;
    }

    private static DockerfileResult RenderDockerfile(TemplateRenderer renderer, PlatformEntry platformEntry)
    {
        if (platformEntry.Platform is not { DockerfileTemplate: string templatePath, Dockerfile: string dockerfilePath })
        {
            return new NoTemplate();
        }

        Dictionary<string, JsonElement> variables = platformEntry.GetVariables();
        RenderResult renderResult = renderer.Render(templatePath, variables);
        return renderResult switch
        {
            RenderedTemplate renderedTemplate => new RenderedDockerfile(dockerfilePath, renderedTemplate.Content),
            TemplateLoadError error => new TemplateError($"{error.TemplatePath}: {error.Message}"),
            TemplateRenderError error => new TemplateError($"{error.TemplatePath} ({platformEntry.Key}): {error.Message}"),
        };
    }

    private static int TestDockerfiles(RenderedDockerfile[] dockerfiles, string manifestDirectory, Stopwatch stopwatch)
    {
        DockerfileCheck[] dockerfileChecks = [.. dockerfiles
            .Select(dockerfile => CheckDockerfile(dockerfile, manifestDirectory))];

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

    private static DockerfileCheck CheckDockerfile(RenderedDockerfile dockerfile, string manifestDirectory)
    {
        string dockerfileFullPath = Path.Join(manifestDirectory, dockerfile.DockerfilePath);
        if (!File.Exists(dockerfileFullPath))
        {
            return new MissingDockerfile(dockerfile.DockerfilePath);
        }

        string existingContent = File.ReadAllText(dockerfileFullPath);

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
