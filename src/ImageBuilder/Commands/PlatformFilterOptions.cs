// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class PlatformFilterOptions
{
    public string Architecture { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
    public IEnumerable<string> OsVersions { get; set; } = [];

}

public class PlatformFilterOptionsBuilder
{
    public const string OsVersionOptionName = "os-version";

    public IEnumerable<Option> GetCliOptions() =>
        [
            CreateOption(
                alias: "architecture",
                propertyName: nameof(PlatformFilterOptions.Architecture),
                description: "Architecture of Dockerfiles to operate on - wildcard chars * and ? supported (default is current OS architecture)",
                defaultValue: () => DockerHelper.Architecture.GetDockerName()),
            CreateOption(
                alias: "os-type",
                propertyName: nameof(PlatformFilterOptions.OsType),
                description: "OS type (linux/windows) of the Dockerfiles to build - wildcard chars * and ? supported (default is the Docker OS)",
                defaultValue: () => DockerHelper.OS.GetDockerName()),
            CreateMultiOption<string>(
                alias: OsVersionOptionName,
                propertyName: nameof(PlatformFilterOptions.OsVersions),
                description: "OS versions of the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)"),
        ];

    public IEnumerable<Argument> GetCliArguments() => [];
}
