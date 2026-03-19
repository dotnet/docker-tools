// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class MergeImageInfoOptions : ManifestOptions
    {
        public string SourceImageInfoFolderPath { get; set; } = string.Empty;

        public string DestinationImageInfoPath { get; set; } = string.Empty;

        public string? InitialImageInfoPath { get; set; }

        public bool IsPublishScenario { get; set; }

        public string? CommitOverride { get; set; }

        private static readonly Argument<string> SourceImageInfoFolderPathArgument = new(nameof(SourceImageInfoFolderPath))
        {
            Description = "Folder path containing image info files"
        };

        private static readonly Argument<string> DestinationImageInfoPathArgument = new(nameof(DestinationImageInfoPath))
        {
            Description = "Path to store the merged image info content"
        };

        private static readonly Option<bool> PublishOption = new(CliHelper.FormatAlias("publish"))
        {
            Description = "Whether the files are being merged as part of publishing to a repo"
        };

        private static readonly Option<string?> InitialImageInfoPathOption = new(CliHelper.FormatAlias("initial-image-info-path"))
        {
            Description = "Path to the image info file to be used as the initial merge target"
        };

        private static readonly Option<string?> CommitOverrideOption = new(CliHelper.FormatAlias("commit-override"))
        {
            Description = "Override the commit in the commitUrl property for images that were updated compared to the"
                + " initial image info"
        };

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            SourceImageInfoFolderPathArgument,
            DestinationImageInfoPathArgument,
        ];

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            PublishOption,
            InitialImageInfoPathOption,
            CommitOverrideOption,
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            SourceImageInfoFolderPath = result.GetValue(SourceImageInfoFolderPathArgument) ?? string.Empty;
            DestinationImageInfoPath = result.GetValue(DestinationImageInfoPathArgument) ?? string.Empty;
            IsPublishScenario = result.GetValue(PublishOption);
            InitialImageInfoPath = result.GetValue(InitialImageInfoPathOption);
            CommitOverride = result.GetValue(CommitOverrideOption);
        }
    }
}
