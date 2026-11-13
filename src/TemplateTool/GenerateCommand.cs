// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

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
        string manifestDirectory = manifestFile.DirectoryName
            ?? throw new ArgumentException($"'{manifestFile}' is not a file path.");
        TemplateRenderer renderer = new(manifestDirectory);

        foreach (ImageEntry imageEntry in manifest.GetImages())
        {
            Console.WriteLine(imageEntry.Key);

            foreach (PlatformEntry platformEntry in imageEntry.GetPlatforms())
            {
                string? templatePath = platformEntry.Platform.DockerfileTemplate;
                string? dockerfilePath = platformEntry.Platform.Dockerfile;
                if (templatePath is null || dockerfilePath is null)
                {
                    Console.WriteLine($"  {platformEntry.PlatformKey}: no template, skipped");
                    continue;
                }

                Dictionary<string, JsonElement> variables = platformEntry.GetVariables();
                string dockerfileContent = await renderer.RenderAsync(templatePath, variables, cancellationToken);
                string dockerfileFullPath = Path.Join(manifestDirectory, dockerfilePath);
                FileInfo dockerfileFile = new(dockerfileFullPath);
                dockerfileFile.Directory?.Create();
                await File.WriteAllTextAsync(dockerfileFile.FullName, dockerfileContent, cancellationToken);
                Console.WriteLine($"  {platformEntry.PlatformKey}: {templatePath} -> {dockerfilePath}");
            }
        }
    }
}
