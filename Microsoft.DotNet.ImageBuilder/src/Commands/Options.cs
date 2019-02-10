// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Reflection;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class Options : IOptionsInfo
    {
        protected abstract string CommandHelp { get; }

        public bool IsDryRun { get; set; }
        public bool IsVerbose { get; set; }
        public string Manifest { get; set; }
        public string RegistryOverride { get; set; }
        public string Repo { get; set; }
        public string RepoPrefix { get; set; }
        public IDictionary<string, string> RepoOverrides { get; set; }
        public IDictionary<string, string> Variables { get; set; }

        protected Options()
        {
        }

        private string GetCommandName()
        {
            string commandName = GetType().Name.TrimEnd("Options");
            return Char.ToLowerInvariant(commandName[0]) + commandName.Substring(1);
        }

        public virtual ManifestFilter GetManifestFilter()
        {
            ManifestFilter filter = new ManifestFilter()
            {
                IncludeRepo = Repo,
            };

            if (this is IFilterableOptions)
            {
                ManifestFilterOptions filterOptions = ((IFilterableOptions)this).FilterOptions;
                filter.IncludeArchitecture = filterOptions.Architecture;
                filter.IncludeOsType = filterOptions.OsType;
                filter.IncludeOsVersion = filterOptions.OsVersion;
                filter.IncludePaths = filterOptions.Paths;
            }

            return filter;
        }

        public string GetOption(string name)
        {
            string result;

            PropertyInfo propInfo = this.GetType().GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
            if (propInfo != null)
            {
                result = propInfo.GetValue(this)?.ToString() ?? "";
            }
            else
            {
                result = null;
            }

            return result;
        }

        public virtual void ParseCommandLine(ArgumentSyntax syntax)
        {
            ArgumentCommand command = syntax.DefineCommand(GetCommandName(), this);
            command.Help = CommandHelp;

            bool isDryRun = false;
            syntax.DefineOption("dry-run", ref isDryRun, "Dry run of what images get built and order they would get built in");
            IsDryRun = isDryRun;

            string manifest = "manifest.json";
            syntax.DefineOption("manifest", ref manifest, "Path to json file which describes the repo");
            Manifest = manifest;

            string registryOverride = null;
            syntax.DefineOption("registry-override", ref registryOverride, "Alternative registry which overrides the manifest");
            RegistryOverride = registryOverride;

            string repo = null;
            syntax.DefineOption("repo", ref repo, "Repo to operate on (Default is all)");
            Repo = repo;

            IReadOnlyList<string> repoOverrides = Array.Empty<string>();
            syntax.DefineOptionList("repo-override", ref repoOverrides, "Alternative repos which overrides the manifest (<target repo>=<override>)");
            RepoOverrides = repoOverrides
                .Select(pair => pair.Split(new char[] { '=' }, 2))
                .ToDictionary(split => split[0], split => split[1]);

            string repoPrefix = null;
            syntax.DefineOption("repo-prefix", ref repoPrefix, "Prefix to add to the repo names specified in the manifest");
            RepoPrefix = repoPrefix;

            IReadOnlyList<string> variables = Array.Empty<string>();
            syntax.DefineOptionList("var", ref variables, "Named variables to substitute into the manifest (<name>=<value>)");
            Variables = variables
                .Select(pair => pair.Split(new char[] { '=' }, 2))
                .ToDictionary(split => split[0], split => split[1]);

            bool isVerbose = false;
            syntax.DefineOption("verbose", ref isVerbose, "Show details about the tasks run");
            IsVerbose = isVerbose;
        }
    }
}
