// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ImageInfoOptions : ManifestOptions
    {
        public string ImageInfoPath { get; set; } = string.Empty;

        private static readonly Argument<string> ImageInfoPathArgument = new("ImageInfoPath")
        {
            Description = "Image info file path"
        };

        public override IEnumerable<Argument> GetCliArguments() =>
            [..base.GetCliArguments(), ImageInfoPathArgument];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            ImageInfoPath = result.GetValue(ImageInfoPathArgument) ?? string.Empty;
        }
    }
}
