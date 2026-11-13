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

        TemplateRenderer renderer = new(manifestDirectory);

        Result<FileCount> result = manifest.GetPlatforms()
            .Select(GetDockerfileTemplate)
            .OfType<DockerfileTemplate>()
            .Select(dockerfileTemplate => RenderDockerfile(renderer, dockerfileTemplate))
            .Sequence()
            .Bind(dockerfiles => test
                ? CheckDockerfiles(dockerfiles, manifestDirectory)
                : WriteDockerfiles(dockerfiles, manifestDirectory));

        return result switch
        {
            FileCount fileCount => ReportSuccess(test, fileCount, stopwatch),
            Failure failure => ReportFailure(failure),
        };
    }

    private static DockerfileTemplate? GetDockerfileTemplate(PlatformEntry platformEntry) =>
        platformEntry.Platform is { DockerfileTemplate: string templatePath, Dockerfile: string dockerfilePath }
            ? new DockerfileTemplate(platformEntry, templatePath, dockerfilePath)
            : null;

    private static Result<RenderedDockerfile> RenderDockerfile(
        TemplateRenderer renderer,
        DockerfileTemplate dockerfileTemplate) =>
        renderer.Load(dockerfileTemplate.TemplatePath)
            .Bind(template => RenderTemplate(template, dockerfileTemplate))
            .Map(content => new RenderedDockerfile(dockerfileTemplate.DockerfilePath, content));

    // Unlike load errors, render errors depend on the platform's variables, so they name the platform.
    private static Result<string> RenderTemplate(IFluidTemplate template, DockerfileTemplate dockerfileTemplate)
    {
        PlatformEntry platformEntry = dockerfileTemplate.PlatformEntry;
        Dictionary<string, JsonElement> variables = platformEntry.GetVariables();
        return TemplateRenderer.Render(template, variables)
            .MapErrors(error => $"{dockerfileTemplate.TemplatePath} ({platformEntry.Key}): {error}");
    }

    private static Result<FileCount> WriteDockerfiles(RenderedDockerfile[] dockerfiles, string manifestDirectory)
    {
        foreach (RenderedDockerfile dockerfile in dockerfiles)
        {
            string dockerfileFullPath = Path.Join(manifestDirectory, dockerfile.DockerfilePath);
            FileInfo dockerfileFile = new(dockerfileFullPath);
            dockerfileFile.Directory?.Create();
            File.WriteAllText(dockerfileFile.FullName, dockerfile.Content);
        }

        return new FileCount(dockerfiles.Length);
    }

    private static Result<FileCount> CheckDockerfiles(RenderedDockerfile[] dockerfiles, string manifestDirectory) =>
        dockerfiles
            .Select(dockerfile => CheckDockerfile(dockerfile, manifestDirectory))
            .Sequence()
            .Map(checkedDockerfiles => new FileCount(checkedDockerfiles.Length));

    private static Result<RenderedDockerfile> CheckDockerfile(RenderedDockerfile dockerfile, string manifestDirectory)
    {
        string dockerfileFullPath = Path.Join(manifestDirectory, dockerfile.DockerfilePath);
        if (!File.Exists(dockerfileFullPath))
        {
            return new Failure($"{dockerfile.DockerfilePath} doesn't exist");
        }

        string existingContent = File.ReadAllText(dockerfileFullPath);

        // Git may check out Dockerfiles with CRLF line endings, so only compare the text.
        string normalizedContent = existingContent.ReplaceLineEndings("\n");
        return normalizedContent == dockerfile.Content
            ? dockerfile
            : new Failure($"{dockerfile.DockerfilePath} doesn't match its template");
    }

    private static int ReportSuccess(bool test, FileCount fileCount, Stopwatch stopwatch)
    {
        string verb = test ? "Checked" : "Generated";
        Console.WriteLine($"{verb} {fileCount.Value} files in {stopwatch.ElapsedMilliseconds}ms");
        return 0;
    }

    private static int ReportFailure(Failure failure)
    {
        foreach (string error in failure.Errors)
        {
            Console.Error.WriteLine($"error: {error}");
        }

        return 1;
    }

    private sealed record DockerfileTemplate(PlatformEntry PlatformEntry, string TemplatePath, string DockerfilePath);

    private sealed record RenderedDockerfile(string DockerfilePath, string Content);

    private readonly record struct FileCount(int Value);
}
