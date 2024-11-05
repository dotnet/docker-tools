// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class MergeImageInfoOptions : ManifestOptions
    {
        public string SourceImageInfoFolderPath { get; set; } = string.Empty;

        public string DestinationImageInfoPath { get; set; } = string.Empty;

        public string? InitialImageInfoPath { get; set; }

        public bool IsPublishScenario { get; set; }
    }

    public class MergeImageInfoOptionsBuilder : ManifestOptionsBuilder
    {
        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                new Argument<string>(nameof(MergeImageInfoOptions.SourceImageInfoFolderPath),
                    "Folder path containing image info files"),
                new Argument<string>(nameof(MergeImageInfoOptions.DestinationImageInfoPath),
                    "Path to store the merged image info content")
            ];

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                CreateOption<bool>("publish", nameof(MergeImageInfoOptions.IsPublishScenario),
                    "Whether the files are being merged as part of publishing to a repo"),
                CreateOption<string?>("initial-image-info-path", nameof(MergeImageInfoOptions.InitialImageInfoPath),
                    "Path to the image info file to be used as the initial merge target"),
            ];
    }
}
