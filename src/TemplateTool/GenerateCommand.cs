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

            // TODO: Render each template with Fluid. For now, this only shows what would be generated.
            foreach ((string key, _, Image image) in manifest.GetImages())
            {
                Console.WriteLine(key);
                foreach ((string platformKey, Platform platform) in image.Platforms)
                {
                    string template = platform.DockerfileTemplate ?? "(no template)";
                    Console.WriteLine($"  {platformKey}: {template} -> {platform.Dockerfile}");
                }
            }
        });

        return command;
    }
}
