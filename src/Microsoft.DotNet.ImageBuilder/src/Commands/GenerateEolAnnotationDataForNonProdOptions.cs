// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

public class GenerateEolAnnotationDataForNonProdOptions : Options
{
    public RegistryOptions RegistryOptions { get; set; } = new();
    public ServiceConnectionOptions? ServiceConnection { get; set; }
}

public class GenerateEolAnnotationDataOptionsForNonProdBuilder : CliOptionsBuilder
{
    private readonly RegistryOptionsBuilder _registryOptionsBuilder = new(isOverride: false);
    private readonly ServiceConnectionOptionsBuilder _serviceConnectionOptionsBuilder = new();

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            .._serviceConnectionOptionsBuilder.GetCliOptions(
                alias: "acr-service-connection",
                propertyName: nameof(GenerateEolAnnotationDataForNonProdOptions.ServiceConnection)),
        ];

    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            .._registryOptionsBuilder.GetCliArguments(),
        ];
}
