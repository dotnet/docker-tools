// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

namespace TemplateTool;

internal static class ListCommand
{
    public static Command Create()
    {
        Argument<FileInfo> manifestArgument = ManifestArgument.Create();
        Command command = new("list", "Lists each image by its key, with its tags.")
        {
            manifestArgument
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            FileInfo manifestFile = parseResult.GetRequiredValue(manifestArgument);
            Manifest manifest = await ManifestLoader.LoadAsync(manifestFile.FullName, cancellationToken);

            foreach (ImageEntry entry in manifest.GetImages())
            {
                Console.WriteLine(entry.Key);
                Dictionary<string, Tag> tags = entry.Image.Tags ?? [];
                foreach ((string name, Tag tag) in tags)
                {
                    string reference = entry.GetReference(name);
                    string line = tag.Undocumented ? $"  {reference} (undocumented)" : $"  {reference}";
                    Console.WriteLine(line);
                }
            }
        });

        return command;
    }
}
