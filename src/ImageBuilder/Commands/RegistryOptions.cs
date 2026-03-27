// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;

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

    private Option<string?>? _registryOption;
    private Option<string?>? _repoPrefixOption;
    private Argument<string>? _registryArgument;
    private Argument<string>? _repoPrefixArgument;

    public IEnumerable<Option> GetCliOptions()
    {
        _registryOption = new Option<string?>(CliHelper.FormatAlias(RegistryOptionName))
        {
            Description = RegistryDescription
        };

        _repoPrefixOption = new Option<string?>(CliHelper.FormatAlias(RepoPrefixOptionName))
        {
            Description = RepoPrefixDescription
        };

        return [_registryOption, _repoPrefixOption];
    }

    public IEnumerable<Argument> GetCliArguments()
    {
        _registryArgument = new Argument<string>(nameof(RegistryOptions.Registry))
        {
            Description = RegistryDescription
        };

        _repoPrefixArgument = new Argument<string>(nameof(RegistryOptions.RepoPrefix))
        {
            Description = RepoPrefixDescription
        };

        return [_registryArgument, _repoPrefixArgument];
    }

    /// <summary>
    /// Binds parsed command line values to the specified <see cref="RegistryOptions"/> instance.
    /// </summary>
    public void Bind(ParseResult result, RegistryOptions target)
    {
        if (_registryOption is not null)
            target.Registry = result.GetValue(_registryOption) ?? string.Empty;
        else if (_registryArgument is not null)
            target.Registry = result.GetValue(_registryArgument) ?? string.Empty;

        if (_repoPrefixOption is not null)
            target.RepoPrefix = result.GetValue(_repoPrefixOption) ?? string.Empty;
        else if (_repoPrefixArgument is not null)
            target.RepoPrefix = result.GetValue(_repoPrefixArgument) ?? string.Empty;
    }
}
