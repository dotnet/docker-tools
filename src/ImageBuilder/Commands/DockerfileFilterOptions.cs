// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class DockerfileFilterOptions
{
    public const string PathOptionName = "path";

    public IEnumerable<string> Paths { get; set; } = [];
    public IEnumerable<string> ProductVersions { get; set; } = [];

    private static readonly Option<string[]> PathsOption = new(CliHelper.FormatAlias(PathOptionName))
    {
        Description = "Directory paths containing the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)",
        DefaultValueFactory = _ => Array.Empty<string>(),
        AllowMultipleArgumentsPerToken = false
    };

    private static readonly Option<string[]> ProductVersionsOption = new(CliHelper.FormatAlias("version"))
    {
        Description = "Product versions of the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)",
        DefaultValueFactory = _ => Array.Empty<string>(),
        AllowMultipleArgumentsPerToken = false
    };

    public IEnumerable<Option> GetCliOptions() =>
        [PathsOption, ProductVersionsOption];

    public IEnumerable<Argument> GetCliArguments() => [];

    public void Bind(ParseResult result)
    {
        Paths = result.GetValue(PathsOption) ?? [];
        ProductVersions = result.GetValue(ProductVersionsOption) ?? [];
    }
}
