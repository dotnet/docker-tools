// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Text;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class RegistryOptions
{
    public string Registry { get; set; } = string.Empty;
    public string RepoPrefix { get; set; } = string.Empty;

    // Example:
    //
    // mcr.microsoft.com/{repoName}@sha256:53af222a293d9d843c966e43feab6167593aeea3ddbac4d45f29e1a463f5c0ca
    // vvvvv
    // {registryOverride}/{repoPrefix}/{repoName}@sha256:53af222a293d9d843c966e43feab6167593aeea3ddbac4d45f29e1a463f5c0ca
    //
    public string ApplyOverrideToDigest(
        string fullyQualifiedDigest,
        string repoName)
    {
        string digest = fullyQualifiedDigest.Split('@')[1];
        string originalRegistry = fullyQualifiedDigest.Split('/')[0];

        string registry = string.IsNullOrEmpty(Registry) ? originalRegistry : Registry;
        registry = registry.Trim('/');

        string repoPrefix = string.IsNullOrEmpty(RepoPrefix) ? string.Empty : RepoPrefix;
        repoPrefix = repoPrefix.Trim('/');

        StringBuilder digestBuilder = new();
        digestBuilder.Append(registry);
        digestBuilder.Append('/');

        if (!string.IsNullOrEmpty(repoPrefix))
        {
            digestBuilder.Append(repoPrefix);
            digestBuilder.Append('/');
        }

        digestBuilder.Append(repoName);
        digestBuilder.Append('@');
        digestBuilder.Append(digest);

        return digestBuilder.ToString();
    }
}

public class RegistryOptionsBuilder(bool isOverride)
{
    private bool _isOverride = isOverride; 

    // Choose one of either CliOptions or CliArguments - not both. Use CliArguments in the case that
    // the registry options are required. Otherwise, use CliOptions.

    private static string RepoPrefixOptionName => "repo-prefix";

    private static string RepoPrefixDescription => "Prefix to add to repo names";

    private string RegistryOptionName => _isOverride ? "registry-override" : "registry";

    private string RegistryDescription => _isOverride
        ? "Registry to use instead of the one specified in the manifest or image info file"
        : "Name of the registry";

    public IEnumerable<Option> GetCliOptions() =>
        [
            CreateOption<string?>(
                alias: RegistryOptionName,
                propertyName: nameof(RegistryOptions.Registry),
                description: RegistryDescription),

            CreateOption<string?>(
                alias: RepoPrefixOptionName,
                propertyName: nameof(RegistryOptions.RepoPrefix),
                description: RepoPrefixDescription),
        ];

    public IEnumerable<Argument> GetCliArguments() =>
        [
            new Argument<string>(
                name: nameof(RegistryOptions.Registry),
                description: RegistryDescription),

            new Argument<string>(
                name: nameof(RegistryOptions.RepoPrefix),
                description: RepoPrefixDescription),
        ];
}
