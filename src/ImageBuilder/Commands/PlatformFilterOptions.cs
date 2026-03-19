// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class PlatformFilterOptions
{
    public const string OsVersionOptionName = "os-version";

    public string Architecture { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
    public IEnumerable<string> OsVersions { get; set; } = [];

    private static readonly Option<string> ArchitectureOption = new(CliHelper.FormatAlias("architecture"))
    {
        Description = "Architecture of Dockerfiles to operate on - wildcard chars * and ? supported (default is current OS architecture)",
        DefaultValueFactory = _ => DockerHelper.Architecture.GetDockerName()
    };

    private static readonly Option<string> OsTypeOption = new(CliHelper.FormatAlias("os-type"))
    {
        Description = "OS type (linux/windows) of the Dockerfiles to build - wildcard chars * and ? supported (default is the Docker OS)",
        DefaultValueFactory = _ => DockerHelper.OS.GetDockerName()
    };

    private static readonly Option<string[]> OsVersionsOption = new(CliHelper.FormatAlias(OsVersionOptionName))
    {
        Description = "OS versions of the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)",
        DefaultValueFactory = _ => Array.Empty<string>(),
        AllowMultipleArgumentsPerToken = false
    };

    public IEnumerable<Option> GetCliOptions() =>
        [ArchitectureOption, OsTypeOption, OsVersionsOption];

    public IEnumerable<Argument> GetCliArguments() => [];

    public void Bind(ParseResult result)
    {
        Architecture = result.GetValue(ArchitectureOption) ?? string.Empty;
        OsType = result.GetValue(OsTypeOption) ?? string.Empty;
        OsVersions = result.GetValue(OsVersionsOption) ?? [];
    }
}
