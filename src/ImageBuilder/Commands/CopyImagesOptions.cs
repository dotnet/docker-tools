// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.Configuration;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

public class CopyImagesOptions : ManifestOptions, IFilterableOptions
{
    public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();
    public ServiceConnection? AcrServiceConnection { get; set; }

    public string ResourceGroup { get; set; } = string.Empty;
    public string Subscription { get; set; } = string.Empty;
}

public class CopyImagesOptionsBuilder : ManifestOptionsBuilder
{
    private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();
    private readonly ServiceConnectionOptionsBuilder _serviceConnectionOptionsBuilder = new();

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
            .._serviceConnectionOptionsBuilder.GetCliOptions(
                alias: "acr-service-connection",
                propertyName: nameof(CopyImagesOptions.AcrServiceConnection)),
            .._manifestFilterOptionsBuilder.GetCliOptions()
        ];

    public override IEnumerable<Argument> GetCliArguments() =>
        [
            ..base.GetCliArguments(),
            .._manifestFilterOptionsBuilder.GetCliArguments(),
            new Argument<string>(nameof(CopyImagesOptions.Subscription),
                "Azure subscription to operate on"),
            new Argument<string>(nameof(CopyImagesOptions.ResourceGroup),
                "Azure resource group to operate on"),
        ];
}
