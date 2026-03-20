// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class CreateManifestListOptions : ManifestOptions, IFilterableOptions
{
    public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();
    public RegistryCredentialsOptions CredentialsOptions { get; set; } = new RegistryCredentialsOptions();
    public string ImageInfoPath { get; set; } = string.Empty;
}

public class CreateManifestListOptionsBuilder : ManifestOptionsBuilder
{
    private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();
    private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();

    public override IEnumerable<Option> GetCliOptions() =>
    [
        ..base.GetCliOptions(),
        .._manifestFilterOptionsBuilder.GetCliOptions(),
        .._registryCredentialsOptionsBuilder.GetCliOptions(),
    ];

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        .._manifestFilterOptionsBuilder.GetCliArguments(),
        .._registryCredentialsOptionsBuilder.GetCliArguments(),
        new Argument<string>(nameof(CreateManifestListOptions.ImageInfoPath),
            "Path to the image info file to read and update with manifest list digests"),
    ];
}
