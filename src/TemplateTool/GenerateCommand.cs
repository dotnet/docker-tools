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
    private static readonly FluidParser Parser = new();

    // Fail on undefined variables and filters instead of rendering empty text or the unfiltered value.
    private static readonly TemplateOptions Options = new() { StrictVariables = true, StrictFilters = true };

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

        Result<Dockerfile[], string[]> result = RenderDockerfiles(manifest, manifestDirectory)
            .Sequence()
            .Bind(dockerfiles => test ? CheckDockerfiles(dockerfiles) : WriteDockerfiles(dockerfiles));

        return result switch
        {
            Dockerfile[] dockerfiles => ReportSuccess(test, dockerfiles, stopwatch),
            Error<string[]> errors => ReportErrors(errors.Value),
        };
    }

    private static IEnumerable<Result<Dockerfile, string>> RenderDockerfiles(Manifest manifest, string manifestDirectory)
    {
        foreach (PlatformEntry platformEntry in manifest.GetPlatforms())
        {
            if (platformEntry.Platform is { DockerfileTemplate: string templatePath, Dockerfile: string dockerfilePath })
            {
                string templateFullPath = Path.Join(manifestDirectory, templatePath);
                string dockerfileFullPath = Path.Join(manifestDirectory, dockerfilePath);
                yield return RenderDockerfile(platformEntry, templateFullPath, dockerfileFullPath);
            }
        }
    }

    private static Result<Dockerfile, string> RenderDockerfile(
        PlatformEntry platformEntry,
        string templatePath,
        string dockerfilePath)
    {
        string templateSource;
        try
        {
            templateSource = File.ReadAllText(templatePath);
        }
        catch (IOException exception)
        {
            return new Error<string>($"{templatePath}: {exception.Message}");
        }

        if (!Parser.TryParse(templateSource, out IFluidTemplate template, out string parseError))
        {
            return new Error<string>($"{templatePath}: {parseError}");
        }

        TemplateContext context = new(Options);
        foreach ((string name, JsonElement value) in platformEntry.GetVariables())
        {
            context.SetValue(name, JsonSerializer.SerializeToNode(value));
        }

        try
        {
            string content = template.Render(context).ReplaceLineEndings("\n");
            return new Dockerfile(dockerfilePath, content);
        }
        catch (FluidException exception)
        {
            return new Error<string>($"{templatePath} ({platformEntry.Key}): {exception.Message}");
        }
    }

    private static Result<Dockerfile[], string[]> WriteDockerfiles(Dockerfile[] dockerfiles)
    {
        foreach (Dockerfile dockerfile in dockerfiles)
        {
            new FileInfo(dockerfile.Path).Directory?.Create();
            File.WriteAllText(dockerfile.Path, dockerfile.Content);
        }

        return dockerfiles;
    }

    private static Result<Dockerfile[], string[]> CheckDockerfiles(Dockerfile[] dockerfiles) =>
        dockerfiles.Select(CheckDockerfile).Sequence();

    private static Result<Dockerfile, string> CheckDockerfile(Dockerfile dockerfile)
    {
        if (!File.Exists(dockerfile.Path))
        {
            return new Error<string>($"{dockerfile.Path} doesn't exist");
        }

        // Git may check out Dockerfiles with CRLF line endings, so only compare the text.
        string existingContent = File.ReadAllText(dockerfile.Path).ReplaceLineEndings("\n");
        return existingContent == dockerfile.Content
            ? dockerfile
            : new Error<string>($"{dockerfile.Path} doesn't match its template");
    }

    private static int ReportSuccess(bool test, Dockerfile[] dockerfiles, Stopwatch stopwatch)
    {
        string verb = test ? "Checked" : "Generated";
        Console.WriteLine($"{verb} {dockerfiles.Length} files in {stopwatch.ElapsedMilliseconds}ms");
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

    private sealed record Dockerfile(string Path, string Content);
}
