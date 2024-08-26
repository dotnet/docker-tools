// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands.Signing;

#nullable enable
public class GenerateSigningPayloadsOptions : Options
{
    public RegistryOptions RegistryOptions { get; set; } = new();
    public RegistryCredentialsOptions RegistryCredentialsOptions { get; set; } = new();

    public string? ImageInfoPath { get; set; }
    public string? PayloadOutputDirectory { get; set; }
}

public class GenerateSigningPayloadsOptionsBuilder : CliOptionsBuilder
{
    private readonly RegistryOptionsBuilder _registryOptionsBuilder = new(isOverride: true);
    private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();

    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            .._registryCredentialsOptionsBuilder.GetCliArguments(),

            new Argument<string>(
                name: nameof(GenerateSigningPayloadsOptions.ImageInfoPath),
                description: "Image info file to generate payloads for"),

            new Argument<string>(
                name: nameof(GenerateSigningPayloadsOptions.PayloadOutputDirectory),
                description: "Directory where signing payloads will be placed"),
        ];

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            .._registryOptionsBuilder.GetCliOptions(),
            .._registryCredentialsOptionsBuilder.GetCliOptions()
        ];
}
