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
            string baseDirectory = manifestFile.DirectoryName!;
            TemplateRenderer renderer = new(baseDirectory);

            foreach (ImageEntry entry in manifest.GetImages())
            {
                Console.WriteLine(entry.Key);
                foreach (PlatformEntry platformEntry in entry.GetPlatforms())
                {
                    Platform platform = platformEntry.Platform;
                    if (platform.DockerfileTemplate is null)
                    {
                        Console.WriteLine($"  {platformEntry.PlatformKey}: no template, skipped");
                        continue;
                    }

                    string dockerfile = await renderer.RenderAsync(
                        platform.DockerfileTemplate, platformEntry.GetVariables(), cancellationToken);
                    string dockerfilePath = Path.Combine(baseDirectory, platform.Dockerfile!);
                    Directory.CreateDirectory(Path.GetDirectoryName(dockerfilePath)!);
                    await File.WriteAllTextAsync(dockerfilePath, dockerfile, cancellationToken);
                    Console.WriteLine($"  {platformEntry.PlatformKey}: {platform.DockerfileTemplate} -> {platform.Dockerfile}");
                }
            }
        });

        return command;
    }
}
