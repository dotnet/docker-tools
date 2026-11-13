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
        Command command = new("list", "Lists each image by its key, with its shared tags and each platform's tags.")
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
            Dictionary<string, Tag> imageTags = imageEntry.Image.Tags ?? [];
            WriteTags(imageEntry, imageTags, indent: "  ");

            foreach (PlatformEntry platformEntry in imageEntry.GetPlatforms())
            {
                Console.WriteLine($"  {platformEntry.Key}");
                Dictionary<string, Tag> platformTags = platformEntry.Platform.Tags ?? [];
                WriteTags(imageEntry, platformTags, indent: "    ");
            }
        }
    }

    // Writes the first tag as a full reference and the rest as tag names, like
    // "mcr.microsoft.com/dotnet/sdk:10.0.403-noble-amd64 [10.0-noble-amd64]".
    private static void WriteTags(ImageEntry imageEntry, Dictionary<string, Tag> tags, string indent)
    {
        List<string> labels = [];
        foreach ((string tagName, Tag tag) in tags)
        {
            string reference = labels.Count == 0 ? imageEntry.GetReference(tagName) : tagName;
            string label = tag.Undocumented ? $"{reference} (undocumented)" : reference;
            labels.Add(label);
        }

        if (labels.Count == 0)
        {
            return;
        }

        if (labels.Count == 1)
        {
            Console.WriteLine($"{indent}{labels[0]}");
            return;
        }

        string otherTags = string.Join(", ", labels.Skip(1));
        Console.WriteLine($"{indent}{labels[0]} [{otherTags}]");
    }
}
