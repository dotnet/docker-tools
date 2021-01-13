// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class MergeImageInfoOptions : ManifestOptions
    {
        public string SourceImageInfoFolderPath { get; set; } = string.Empty;

        public string DestinationImageInfoPath { get; set; } = string.Empty;
    }

    public class MergeImageInfoSymbolsBuilder : ManifestSymbolsBuilder
    {
        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(
                    new Argument[]
                    {
                        new Argument<string>(nameof(MergeImageInfoOptions.SourceImageInfoFolderPath), "Folder path containing image info files"),
                        new Argument<string>(nameof(MergeImageInfoOptions.DestinationImageInfoPath), "Path to store the merged image info content"),
                    }
                );
    }
}
#nullable disable
