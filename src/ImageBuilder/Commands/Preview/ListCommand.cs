// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.IO;
using Microsoft.DotNet.ImageBuilder.Models.V2;
using Microsoft.DotNet.ImageBuilder.Models.V2.Projections;

namespace Microsoft.DotNet.ImageBuilder.Commands.Preview;

public class ListCommand(TextWriter output) : ICommand
{
    private static readonly Argument<string> ManifestArgument = new("Manifest")
    {
        Description = "Path to the manifest file"
    };

    private readonly TextWriter _output = output;

    public Command GetCliCommand()
    {
        Command command = new("list", "Lists the tags in a V2 manifest (preview)")
        {
            ManifestArgument
        };

        command.SetAction(parseResult => List(parseResult.GetRequiredValue(ManifestArgument)));
        return command;
    }

    private void List(string manifestPath)
    {
        Manifest manifest = ManifestSerializer.Load(manifestPath);

        foreach (ImageEntry imageEntry in manifest.GetAllImageEntries())
        {
            _output.WriteLine(imageEntry.Key);

            foreach (ImageReference reference in imageEntry.GetSharedReferences())
            {
                _output.WriteLine($"- {reference}");
            }

            foreach (PlatformEntry platformEntry in imageEntry.GetPlatformEntries())
            {
                _output.WriteLine($"- {platformEntry.Key.Platform} ({platformEntry.ImagePlatform.Platform})");

                foreach (ImageReference reference in platformEntry.GetPlatformReferences())
                {
                    _output.WriteLine($"  - {reference}");
                }
            }
        }
    }
}
