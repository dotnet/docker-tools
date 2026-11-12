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
            string registryPrefix = manifest.Registry is null ? "" : $"{manifest.Registry}/";

            foreach ((string key, Repo repo, Image image) in manifest.GetImages())
            {
                Console.WriteLine(key);
                foreach ((string name, Tag tag) in image.Tags ?? [])
                {
                    string reference = $"{registryPrefix}{repo.Name}:{name}";
                    Console.WriteLine(tag.Undocumented ? $"  {reference} (undocumented)" : $"  {reference}");
                }
            }
        });

        return command;
    }
}
