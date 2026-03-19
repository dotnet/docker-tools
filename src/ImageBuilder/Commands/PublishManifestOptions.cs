// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestOptions : ManifestOptions, IFilterableOptions
    {
        public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();
        public RegistryCredentialsOptions CredentialsOptions { get; set; } = new RegistryCredentialsOptions();

        public string ImageInfoPath { get; set; } = string.Empty;

        private static readonly Argument<string> ImageInfoPathArgument = new(nameof(ImageInfoPath))
        {
            Description = "Image info file path"
        };

        public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            ..FilterOptions.GetCliOptions(),
            ..CredentialsOptions.GetCliOptions(),
        ];

        public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            ..FilterOptions.GetCliArguments(),
            ..CredentialsOptions.GetCliArguments(),
            ImageInfoPathArgument,
        ];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            FilterOptions.Bind(result);
            CredentialsOptions.Bind(result);
            ImageInfoPath = result.GetValue(ImageInfoPathArgument) ?? string.Empty;
        }
    }
}
