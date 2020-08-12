// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ManifestOptions : Options, IManifestOptionsInfo
    {
        public string Manifest { get; set; }
        public string RegistryOverride { get; set; }
        public IEnumerable<string> Repos { get; set; }
        public string RepoPrefix { get; set; }
        public IDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        protected ManifestOptions()
        {
        }

        public virtual ManifestFilter GetManifestFilter()
        {
            ManifestFilter filter = new ManifestFilter()
            {
                IncludeRepos = Repos,
            };

            if (this is IFilterableOptions)
            {
                ManifestFilterOptions filterOptions = ((IFilterableOptions)this).FilterOptions;
                filter.IncludeArchitecture = filterOptions.Architecture;
                filter.IncludeOsType = filterOptions.OsType;
                filter.IncludeOsVersions = filterOptions.OsVersions;
                filter.IncludePaths = filterOptions.Paths;
            }

            return filter;
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            string manifest = "manifest.json";
            syntax.DefineOption("manifest", ref manifest, "Path to json file which describes the repo");
            Manifest = manifest;

            string registryOverride = null;
            syntax.DefineOption("registry-override", ref registryOverride, "Alternative registry which overrides the manifest");
            RegistryOverride = registryOverride;

            IReadOnlyList<string> repos = Array.Empty<string>();
            syntax.DefineOptionList("repo", ref repos, "Repos to operate on (Default is all)");
            Repos = repos;

            string repoPrefix = null;
            syntax.DefineOption("repo-prefix", ref repoPrefix, "Prefix to add to the repo names specified in the manifest");
            RepoPrefix = repoPrefix;

            IReadOnlyList<string> variables = Array.Empty<string>();
            syntax.DefineOptionList("var", ref variables, "Named variables to substitute into the manifest (<name>=<value>)");
            Variables = variables
                .Select(pair => pair.Split(new char[] { '=' }, 2))
                .ToDictionary(split => split[0], split => split[1]);
        }
    }
}
