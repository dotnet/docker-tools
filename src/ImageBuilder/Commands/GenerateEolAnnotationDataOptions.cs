// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Configuration;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

public class GenerateEolAnnotationDataOptions : Options
{
    public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();
    public RegistryOptions RegistryOptions { get; set; } = new();
    public ServiceConnection? AcrServiceConnection { get; set; }
    public string EolDigestsListPath { get; set; } = string.Empty;
}

public class GenerateEolAnnotationDataOptionsBuilder : CliOptionsBuilder
{
    private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();
    private readonly RegistryOptionsBuilder _registryOptionsBuilder = new(isOverride: false);
    private readonly ServiceConnectionOptionsBuilder _serviceConnectionOptionsBuilder = new();

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            .._registryCredentialsOptionsBuilder.GetCliOptions(),
            .._serviceConnectionOptionsBuilder.GetCliOptions(
                alias: "acr-service-connection",
                propertyName: nameof(GenerateEolAnnotationDataOptions.AcrServiceConnection)),
        ];

    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            .._registryCredentialsOptionsBuilder.GetCliArguments(),
            .._registryOptionsBuilder.GetCliArguments(),
            new Argument<string>(nameof(GenerateEolAnnotationDataOptions.EolDigestsListPath),
                "EOL annotations digests list output path"),
        ];
}
