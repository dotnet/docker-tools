// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands;

#nullable enable
public class RegistryOverrideOptions
{
    public string RegistryOverride { get; set; } = string.Empty;
    public string RepoPrefix { get; set; } = string.Empty;

    public const string RegistryOverrideName = "registry-override";
    public const string RepoPrefixName = "repo-prefix";

    // Example:
    // mcr.microsoft.com/{repoName}@sha256:53af222a293d9d843c966e43feab6167593aeea3ddbac4d45f29e1a463f5c0ca
    // Should be converted to:
    // {registryOverride}/{repoPrefix}/{repoName}@sha256:53af222a293d9d843c966e43feab6167593aeea3ddbac4d45f29e1a463f5c0ca
    public string ApplyToDigest(
        string fullyQualifiedDigest,
        string repoName)
    {
        string digest = fullyQualifiedDigest.Split('@')[1];
        string repoPrefix = string.IsNullOrEmpty(RepoPrefix) ? string.Empty : RepoPrefix + '/';

        return $"{RegistryOverride}/{repoPrefix}{repoName}@{digest}";
    }
}

public class RegistryOverrideOptionsBuilder
{
    public IEnumerable<Option> GetCliOptions() =>
        [
            CreateOption<string?>(
                alias: RegistryOverrideOptions.RegistryOverrideName,
                propertyName: nameof(RegistryOverrideOptions.RegistryOverride),
                description: $"Registry to use instead of the one specified in the manifest or image info file"),

            CreateOption<string?>(
                alias: RegistryOverrideOptions.RepoPrefixName,
                propertyName: nameof(RegistryOverrideOptions.RepoPrefix),
                description: "Prefix to add to the repo names specified in the manifest"),
        ];

    public IEnumerable<Argument> GetCliArguments() => [];
}
