// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
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

    public abstract class ManifestOptionsBuilder : CliOptionsBuilder
    {
        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions().Concat(
                new Option[]
                {
                    CreateOption("manifest", nameof(ManifestOptions.Manifest),
                        "Path to json file which describes the repo", "manifest.json"),
                    CreateOption<string?>(ManifestOptions.RegistryOverrideName, nameof(ManifestOptions.RegistryOverride),
                        "Alternative registry which overrides the manifest"),
                    CreateMultiOption<string>("repo", nameof(ManifestOptions.Repos),
                        "Repos to operate on (Default is all)"),
                    CreateOption<string?>("repo-prefix", nameof(ManifestOptions.RepoPrefix),
                        "Prefix to add to the repo names specified in the manifest"),
                    CreateDictionaryOption("var", nameof(ManifestOptions.Variables),
                        "Named variables to substitute into the manifest (<name>=<value>)")
                });
    }
}
