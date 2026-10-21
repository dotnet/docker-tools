// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class CreateManifestListOptions : ManifestOptions, IFilterableOptions
{
    public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();
    public RegistryCredentialsOptions CredentialsOptions { get; set; } = new RegistryCredentialsOptions();
    public string ImageInfoPath { get; set; } = string.Empty;

    private static readonly Argument<string> ImageInfoPathArgument = new(nameof(ImageInfoPath))
    {
        Description = "Path to the image info file to read and update with manifest list digests"
    };

    public override IEnumerable<Option> GetCliOptions() =>
    [
        ..base.GetCliOptions(),
        ..ManifestFilterOptions.GetCliOptions(),
        ..RegistryCredentialsOptions.GetCliOptions(),
    ];

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        ..ManifestFilterOptions.GetCliArguments(),
        ..RegistryCredentialsOptions.GetCliArguments(),
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
