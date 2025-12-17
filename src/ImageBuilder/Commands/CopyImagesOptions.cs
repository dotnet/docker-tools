// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

public class CopyImagesOptions : ManifestOptions, IFilterableOptions
{
    public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

    public string ResourceGroup { get; set; } = string.Empty;
    public string Subscription { get; set; } = string.Empty;
}

public class CopyImagesOptionsBuilder : ManifestOptionsBuilder
{
    private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();

    public override IEnumerable<Option> GetCliOptions() =>
        [
            ..base.GetCliOptions(),
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
