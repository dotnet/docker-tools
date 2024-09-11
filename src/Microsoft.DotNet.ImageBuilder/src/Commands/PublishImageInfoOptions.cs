// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishImageInfoOptions : ImageInfoOptions, IGitOptionsHost
    {
        public GitOptions GitOptions { get; set; } = new GitOptions();

        public string? OriginalImageInfoOutputPath { get; set; }
        public string? UpdatedImageInfoOutputPath { get; set; }
    }

    public class PublishImageInfoOptionsBuilder : ImageInfoOptionsBuilder
    {
        private readonly GitOptionsBuilder _gitOptionsBuilder = GitOptionsBuilder.BuildWithDefaults();

        public override IEnumerable<Option> GetCliOptions() =>
            [
                ..base.GetCliOptions(),
                .._gitOptionsBuilder.GetCliOptions(),
                CreateOption<string?>("image-info-orig-path", nameof(PublishImageInfoOptions.OriginalImageInfoOutputPath),
                    $"Path where the original image info content will be written to"),
                CreateOption<string?>("image-info-update-path", nameof(PublishImageInfoOptions.UpdatedImageInfoOutputPath),
                    $"Path where the updated image info content will be written to"),

            ];

        public override IEnumerable<Argument> GetCliArguments() =>
            [
                ..base.GetCliArguments(),
                .._gitOptionsBuilder.GetCliArguments()
            ];
    }
}
