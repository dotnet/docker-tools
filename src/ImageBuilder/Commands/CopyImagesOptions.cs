// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class CopyImagesOptions : ManifestOptions, IFilterableOptions
{
    public ManifestFilterOptions FilterOptions { get; set; } = new ManifestFilterOptions();

    public override IEnumerable<Option> GetCliOptions() =>
    [
        ..base.GetCliOptions(),
        ..ManifestFilterOptions.GetCliOptions(),
    ];

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        ..ManifestFilterOptions.GetCliArguments(),
    ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        FilterOptions.Bind(result);
    }
}
