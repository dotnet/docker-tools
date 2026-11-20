// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.V2;

namespace Microsoft.DotNet.ImageBuilder.Commands.Preview;

public class MergeCommand(TextWriter output) : ICommand
{
    private static readonly Option<string> InputOption = new("--input")
    {
        Description = "Directory containing the V2 image artifact details files to merge",
        Required = true,
    };

    private static readonly Option<string> OutputOption = new("--output")
    {
        Description = "Path to write the merged image artifact details file",
        Required = true,
    };

    private readonly TextWriter _output = output;

    public Command GetCliCommand()
    {
        Command command = new("merge", "Merges multiple ImageArtifactDetails JSON files (preview)")
        {
            InputOption,
            OutputOption
        };

        command.SetAction(parseResult =>
        {
            string inputDirectory = Path.GetFullPath(parseResult.GetRequiredValue(InputOption));
            string outputPath = Path.GetFullPath(parseResult.GetRequiredValue(OutputOption));
            MergeFiles(inputDirectory, outputPath);
        });

        return command;
    }

    private void MergeFiles(string inputDirectory, string outputPath)
    {
        string[] inputFiles = Directory
            .EnumerateFiles(inputDirectory, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (inputFiles.Length == 0)
        {
            throw new InvalidOperationException($"No JSON files found in input directory '{inputDirectory}'.");
        }

        _output.WriteLine("Merging files:");
        foreach (string inputFile in inputFiles)
        {
            _output.WriteLine($"- {inputFile}");
        }

        IEnumerable<ImageArtifactDetails> inputs = inputFiles.Select(ImageArtifactDetailsSerializer.Load);
        ImageArtifactDetails merged = ImageArtifactDetails.Merge(inputs);
        string mergedContent = ImageArtifactDetailsSerializer.Serialize(merged);

        File.WriteAllText(outputPath, mergedContent);
        _output.WriteLine($"Wrote {new FileInfo(outputPath).Length} bytes to {outputPath}");
    }
}
