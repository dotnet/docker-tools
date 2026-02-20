// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands.Signing;

public class SignImagesOptions : Options
{
    public string ImageInfoPath { get; set; } = string.Empty;
    public RegistryOptions RegistryOverride { get; set; } = new();
}

public class SignImagesOptionsBuilder : CliOptionsBuilder
{
    private readonly RegistryOptionsBuilder _registryOptionsBuilder = new(isOverride: true);

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        new Argument<string>(
            name: nameof(SignImagesOptions.ImageInfoPath),
            description: "Path to merged image info file containing images to sign")
    ];

    public override IEnumerable<Option> GetCliOptions() =>
    [
        ..base.GetCliOptions(),
        .._registryOptionsBuilder.GetCliOptions()
    ];
}
