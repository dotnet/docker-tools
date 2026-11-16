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

        TemplateRenderer renderer = new();

        Result<FileCount, string[]> result = manifest.GetPlatforms()
            .Select(platformEntry => GetDockerfileTemplate(platformEntry, manifestDirectory))
            .OfType<DockerfileTemplate>()
            .Select(dockerfileTemplate => RenderDockerfile(renderer, dockerfileTemplate))
            .Sequence()
            .Bind(dockerfiles => test
                ? CheckDockerfiles(dockerfiles)
                : WriteDockerfiles(dockerfiles));

        return result switch
        {
            Ok<FileCount> fileCount => ReportSuccess(test, fileCount.Value, stopwatch),
            Error<string[]> errors => ReportErrors(errors.Value),
        };
    }

    private static DockerfileTemplate? GetDockerfileTemplate(PlatformEntry platformEntry, string manifestDirectory)
    {
        if (platformEntry.Platform is not { DockerfileTemplate: string templatePath, Dockerfile: string dockerfilePath })
        {
            return null;
        }

        string templateFullPath = Path.Join(manifestDirectory, templatePath);
        string dockerfileFullPath = Path.Join(manifestDirectory, dockerfilePath);
        return new DockerfileTemplate(platformEntry, templateFullPath, dockerfileFullPath);
    }

    private static Result<RenderedDockerfile, string> RenderDockerfile(
        TemplateRenderer renderer,
        DockerfileTemplate dockerfileTemplate) =>
        renderer.Load(dockerfileTemplate.TemplateFullPath)
            .MapError(error => $"{dockerfileTemplate.TemplateFullPath}: {error}")
            .Bind(template => RenderTemplate(template, dockerfileTemplate))
            .Map(content => new RenderedDockerfile(dockerfileTemplate.DockerfileFullPath, content));

    // Unlike load errors, render errors depend on the platform's variables, so they name the platform.
    private static Result<string, string> RenderTemplate(IFluidTemplate template, DockerfileTemplate dockerfileTemplate)
    {
        PlatformEntry platformEntry = dockerfileTemplate.PlatformEntry;
        Dictionary<string, JsonElement> variables = platformEntry.GetVariables();
        return TemplateRenderer.Render(template, variables)
            .MapError(error => $"{dockerfileTemplate.TemplateFullPath} ({platformEntry.Key}): {error}");
    }

    private static Result<FileCount, string[]> WriteDockerfiles(RenderedDockerfile[] dockerfiles)
    {
        foreach (RenderedDockerfile dockerfile in dockerfiles)
        {
            FileInfo dockerfileFile = new(dockerfile.DockerfileFullPath);
            dockerfileFile.Directory?.Create();
            File.WriteAllText(dockerfileFile.FullName, dockerfile.Content);
        }

        return new Ok<FileCount>(new FileCount(dockerfiles.Length));
    }

    private static Result<FileCount, string[]> CheckDockerfiles(RenderedDockerfile[] dockerfiles) =>
        dockerfiles
            .Select(CheckDockerfile)
            .Sequence()
            .Map(checkedDockerfiles => new FileCount(checkedDockerfiles.Length));

    private static Result<RenderedDockerfile, string> CheckDockerfile(RenderedDockerfile dockerfile)
    {
        if (!File.Exists(dockerfile.DockerfileFullPath))
        {
            return new Error<string>($"{dockerfile.DockerfileFullPath} doesn't exist");
        }

        string existingContent = File.ReadAllText(dockerfile.DockerfileFullPath);

        // Git may check out Dockerfiles with CRLF line endings, so only compare the text.
        string normalizedContent = existingContent.ReplaceLineEndings("\n");
        return normalizedContent == dockerfile.Content
            ? new Ok<RenderedDockerfile>(dockerfile)
            : new Error<string>($"{dockerfile.DockerfileFullPath} doesn't match its template");
    }

    private static int ReportSuccess(bool test, FileCount fileCount, Stopwatch stopwatch)
    {
        string verb = test ? "Checked" : "Generated";
        Console.WriteLine($"{verb} {fileCount.Value} files in {stopwatch.ElapsedMilliseconds}ms");
        return 0;
    }

    // Distinct, because a template that fails to load fails the same way for every platform that uses it.
    private static int ReportErrors(string[] errors)
    {
        foreach (string error in errors.Distinct())
        {
            Console.Error.WriteLine($"error: {error}");
        }

        return 1;
    }

    private sealed record DockerfileTemplate(PlatformEntry PlatformEntry, string TemplateFullPath, string DockerfileFullPath);

    private sealed record RenderedDockerfile(string DockerfileFullPath, string Content);

    private readonly record struct FileCount(int Value);
}
