// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class ManifestFilterOptions
{
    public PlatformFilterOptions Platform { get; set; } = new();

    public DockerfileFilterOptions Dockerfile { get; set; } = new();

    public static IEnumerable<Option> GetCliOptions() =>
    [
        ..PlatformFilterOptions.GetCliOptions(),
        ..DockerfileFilterOptions.GetCliOptions(),
    ];

    public static IEnumerable<Argument> GetCliArguments() =>
    [
        ..PlatformFilterOptions.GetCliArguments(),
        ..DockerfileFilterOptions.GetCliArguments(),
    ];

    public void Bind(ParseResult result)
    {
        Platform.Bind(result);
        Dockerfile.Bind(result);
    }
}
