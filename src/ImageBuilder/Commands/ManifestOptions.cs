// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ManifestOptions : Options, IManifestOptionsInfo
    {
        public const string RegistryOverrideName = "registry-override";

        public string Manifest { get; set; } = string.Empty;
        public string? RegistryOverride { get; set; }
        public IEnumerable<string> Repos { get; set; } = Enumerable.Empty<string>();
        public string? RepoPrefix { get; set; }
        public IDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        private static readonly Option<string> ManifestOption = new(CliHelper.FormatAlias("manifest"))
        {
            Description = "Path to json file which describes the repo",
            DefaultValueFactory = _ => "manifest.json"
        };

        private static readonly Option<string?> RegistryOverrideOption = new(CliHelper.FormatAlias(RegistryOverrideName))
        {
            Description = "Alternative registry which overrides the manifest"
        };

        private static readonly Option<string[]> ReposOption = new(CliHelper.FormatAlias("repo"))
        {
            Description = "Repos to operate on (Default is all)",
            DefaultValueFactory = _ => Array.Empty<string>(),
            AllowMultipleArgumentsPerToken = false
        };

        private static readonly Option<string?> RepoPrefixOption = new(CliHelper.FormatAlias("repo-prefix"))
        {
            Description = "Prefix to add to the repo names specified in the manifest"
        };

        private static readonly Option<Dictionary<string, string>> VariablesOption =
            CliHelper.CreateDictionaryOption("var",
                "Named variables to substitute into the manifest (<name>=<value>)");

        public override IEnumerable<Option> GetCliOptions() =>
            [..base.GetCliOptions(), ManifestOption, RegistryOverrideOption, ReposOption, RepoPrefixOption, VariablesOption];

        public override void Bind(ParseResult result)
        {
            base.Bind(result);
            Manifest = result.GetValue(ManifestOption) ?? string.Empty;
            RegistryOverride = result.GetValue(RegistryOverrideOption);
            Repos = result.GetValue(ReposOption) ?? [];
            RepoPrefix = result.GetValue(RepoPrefixOption);
            Variables = result.GetValue(VariablesOption) ?? new Dictionary<string, string>();
        }

        public virtual ManifestFilter GetManifestFilter()
        {
            ManifestFilter filter = new(Repos);

            if (this is IFilterableOptions options)
            {
                filter = SetPlatformFilters(filter, options.FilterOptions.Platform);
                filter = SetDockerfileFilters(filter, options.FilterOptions.Dockerfile);
            }

            if (this is IPlatformFilterableOptions platformFilterOptions)
            {
                filter = SetPlatformFilters(filter, platformFilterOptions.Platform);
            }

            if (this is IDockerfileFilterableOptions dockerfileFilterOptions)
            {
                filter = SetDockerfileFilters(filter, dockerfileFilterOptions.Dockerfile);
            }

            return filter;
        }

        private static ManifestFilter SetDockerfileFilters(ManifestFilter filter, DockerfileFilterOptions options)
        {
            filter.IncludePaths = options.Paths;
            filter.IncludeProductVersions = options.ProductVersions;
            return filter;
        }

        private static ManifestFilter SetPlatformFilters(ManifestFilter filter, PlatformFilterOptions options)
        {
            filter.IncludeArchitecture = options.Architecture;
            filter.IncludeOsType = options.OsType;
            filter.IncludeOsVersions = options.OsVersions;
            return filter;
        }
    }
}
