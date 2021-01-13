// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ManifestOptions : Options, IManifestOptionsInfo
    {
        public string Manifest { get; set; } = string.Empty;
        public string? RegistryOverride { get; set; }
        public IEnumerable<string> Repos { get; set; } = Enumerable.Empty<string>();
        public string? RepoPrefix { get; set; }
        public IDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        public virtual ManifestFilter GetManifestFilter()
        {
            ManifestFilter filter = new ManifestFilter()
            {
                IncludeRepos = Repos,
            };

            if (this is IFilterableOptions options)
            {
                ManifestFilterOptions filterOptions = options.FilterOptions;
                filter.IncludeArchitecture = filterOptions.Architecture;
                filter.IncludeOsType = filterOptions.OsType;
                filter.IncludeOsVersions = filterOptions.OsVersions;
                filter.IncludePaths = filterOptions.Paths;
            }

            return filter;
        }
    }

    public abstract class ManifestSymbolsBuilder : CliSymbolsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions().Concat(
                new Option[]
                {
                    new Option<string>("--manifest", () => "manifest.json", "Path to json file which describes the repo")
                    {
                        Name = nameof(ManifestOptions.Manifest)
                    },
                    new Option<string?>("--registry-override", "Alternative registry which overrides the manifest")
                    {
                        Name = nameof(ManifestOptions.RegistryOverride)
                    },
                    new Option<string[]>("--repo", () => Array.Empty<string>(), "Repos to operate on (Default is all)")
                    {
                        Name = nameof(ManifestOptions.Repos)
                    },
                    new Option<string?>("--repo-prefix", "Prefix to add to the repo names specified in the manifest")
                    {
                        Name = nameof(ManifestOptions.RepoPrefix)
                    },
                    new Option<Dictionary<string, string>>("--var", description: "Named variables to substitute into the manifest (<name>=<value>)",
                        parseArgument: argResult =>
                        {
                            return argResult.Tokens
                                .ToList()
                                .Select(token => token.Value.Split(new char[] { '=' }, 2))
                                .ToDictionary(split => split[0], split => split[1]);
                        })
                    {
                        Name = nameof(ManifestOptions.Variables)
                    },
                });
    }
}
#nullable disable
