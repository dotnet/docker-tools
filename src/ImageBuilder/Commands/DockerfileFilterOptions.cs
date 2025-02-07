// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

public class DockerfileFilterOptions
{
    public IEnumerable<string> Paths { get; set; } = [];
    public IEnumerable<string> ProductVersions { get; set; } = [];
}

public class DockerfileFilterOptionsBuilder
{
    public const string PathOptionName = "path";

    public IEnumerable<Option> GetCliOptions() =>
        [
            CreateMultiOption<string>(
                alias: PathOptionName,
                propertyName: nameof(DockerfileFilterOptions.Paths),
                description:
                    "Directory paths containing the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)"),
            CreateMultiOption<string>(
                alias: "version",
                propertyName: nameof(DockerfileFilterOptions.ProductVersions),
                description: "Product versions of the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)")
        ];

    public IEnumerable<Argument> GetCliArguments() => [];
}
