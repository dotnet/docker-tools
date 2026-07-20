// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class CheckBaseImagesOptions : ImageInfoOptions, IFilterableOptions
{
    public ManifestFilterOptions FilterOptions { get; set; } = new();

    public RegistryCredentialsOptions CredentialsOptions { get; set; } = new();

    public BaseImageOverrideOptions BaseImageOverrideOptions { get; set; } = new();

    public string? SourceRepoPrefix { get; set; }

    private static readonly Option<string?> SourceRepoPrefixOption = new("--source-repo-prefix")
    {
        Description = "Repo prefix used to locate mirrored external base images in the overridden registry"
    };

    public override IEnumerable<Option> GetCliOptions() =>
    [
        ..base.GetCliOptions(),
        ..FilterOptions.GetCliOptions(),
        ..CredentialsOptions.GetCliOptions(),
        ..BaseImageOverrideOptions.GetCliOptions(),
        SourceRepoPrefixOption,
    ];

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        ..FilterOptions.GetCliArguments(),
        ..CredentialsOptions.GetCliArguments(),
        ..BaseImageOverrideOptions.GetCliArguments(),
    ];

    public override void Bind(ParseResult result)
    {
        base.Bind(result);
        FilterOptions.Bind(result);
        CredentialsOptions.Bind(result);
        BaseImageOverrideOptions.Bind(result);
        SourceRepoPrefix = result.GetValue(SourceRepoPrefixOption);
    }
}
