// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            FileInfo manifestFile = parseResult.GetRequiredValue(manifestArgument);
            Manifest manifest = await ManifestLoader.LoadAsync(manifestFile.FullName, cancellationToken);
            string baseDirectory = manifestFile.DirectoryName
                ?? throw new ArgumentException($"'{manifestFile}' is not a file path.");
            TemplateRenderer renderer = new(baseDirectory);

            foreach (ImageEntry entry in manifest.GetImages())
            {
                Console.WriteLine(entry.Key);
                foreach (PlatformEntry platformEntry in entry.GetPlatforms())
                {
                    if (platformEntry.Platform is not { DockerfileTemplate: { } template, Dockerfile: { } dockerfile })
                    {
                        Console.WriteLine($"  {platformEntry.PlatformKey}: no template, skipped");
                        continue;
                    }

                    string content = await renderer.RenderAsync(template, platformEntry.GetVariables(), cancellationToken);
                    FileInfo output = new(Path.Join(baseDirectory, dockerfile));
                    output.Directory?.Create();
                    await File.WriteAllTextAsync(output.FullName, content, cancellationToken);
                    Console.WriteLine($"  {platformEntry.PlatformKey}: {template} -> {dockerfile}");
                }
            }
        });

        return command;
    }
}
