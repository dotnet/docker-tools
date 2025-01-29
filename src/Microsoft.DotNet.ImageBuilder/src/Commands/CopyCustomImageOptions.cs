// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

public class CopyCustomImageOptions : Options
{
    public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();

    public string ImageName { get; set; } = string.Empty;
    public string Subscription { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string DestinationRegistry { get; set; } = string.Empty;
}

public class CopyCustomImageOptionsBuilder : CliOptionsBuilder
{
    private readonly RegistryCredentialsOptionsBuilder _registryCredentialsOptionsBuilder = new();

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            .._registryCredentialsOptionsBuilder.GetCliOptions(),
        ];

    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            .._registryCredentialsOptionsBuilder.GetCliArguments(),
            new Argument<string>(nameof(CopyCustomImageOptions.Subscription),
                "Azure subscription to operate on"),
            new Argument<string>(nameof(CopyCustomImageOptions.ResourceGroup),
                "Azure resource group to operate on"),
            new Argument<string>(nameof(CopyCustomImageOptions.ImageName),
                "Name of the image to be imported"),
            new Argument<string>(nameof(CopyCustomImageOptions.DestinationRegistry),
                "Name of the destination registry"),
        ];
}
