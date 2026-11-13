// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

namespace TemplateTool;

internal static class GenerateCommand
{
    public static Command Create()
    {
        Argument<FileInfo> manifestArgument = ManifestArgument.Create();
        Command command = new("generate", "Generates each platform's Dockerfile from its template.")
        {
            manifestArgument
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            FileInfo manifestFile = parseResult.GetRequiredValue(manifestArgument);
            return GenerateAsync(manifestFile, cancellationToken);
        });

        return command;
    }

    private static async Task GenerateAsync(FileInfo manifestFile, CancellationToken cancellationToken)
    {
        Manifest manifest = await ManifestLoader.LoadAsync(manifestFile.FullName, cancellationToken);
        string baseDirectory = manifestFile.DirectoryName
            ?? throw new ArgumentException($"'{manifestFile}' is not a file path.");
        TemplateRenderer renderer = new(baseDirectory);

        foreach (ImageEntry image in manifest.GetImages())
        {
            Console.WriteLine(image.Key);

            foreach (PlatformEntry platformEntry in image.GetPlatforms())
            {
                string? template = platformEntry.Platform.DockerfileTemplate;
                string? dockerfile = platformEntry.Platform.Dockerfile;
                if (template is null || dockerfile is null)
                {
                    Console.WriteLine($"  {platformEntry.PlatformKey}: no template, skipped");
                    continue;
                }

                Dictionary<string, JsonElement> variables = platformEntry.GetVariables();
                string content = await renderer.RenderAsync(template, variables, cancellationToken);
                string outputPath = Path.Join(baseDirectory, dockerfile);
                FileInfo output = new(outputPath);
                output.Directory?.Create();
                await File.WriteAllTextAsync(output.FullName, content, cancellationToken);
                Console.WriteLine($"  {platformEntry.PlatformKey}: {template} -> {dockerfile}");
            }
        }
    }
}
