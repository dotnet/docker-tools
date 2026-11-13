// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

internal static class ListCommand
{
    public static Command Create()
    {
        Argument<FileInfo> manifestArgument = ManifestArgument.Create();
        Command command = new("list", "Lists each image by its key, with its tags.")
        {
            manifestArgument
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            FileInfo manifestFile = parseResult.GetRequiredValue(manifestArgument);
            return ListAsync(manifestFile, cancellationToken);
        });

        return command;
    }

    private static async Task ListAsync(FileInfo manifestFile, CancellationToken cancellationToken)
    {
        Manifest manifest = await ManifestLoader.LoadAsync(manifestFile.FullName, cancellationToken);

        foreach (ImageEntry imageEntry in manifest.GetImages())
        {
            Console.WriteLine(imageEntry.Key);
            Dictionary<string, Tag> tags = imageEntry.Image.Tags ?? [];
            foreach ((string tagName, Tag tag) in tags)
            {
                string reference = imageEntry.GetReference(tagName);
                string line = tag.Undocumented ? $"  {reference} (undocumented)" : $"  {reference}";
                Console.WriteLine(line);
            }
        }
    }
}
