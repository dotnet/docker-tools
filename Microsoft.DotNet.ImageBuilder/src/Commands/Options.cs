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
        protected abstract string CommandName { get; }

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

        protected static Architecture DefineArchitectureOption(ArgumentSyntax syntax)
        {
            Architecture architecture = DockerHelper.Architecture;
            syntax.DefineOption(
                "architecture",
                ref architecture,
                value => (Architecture)Enum.Parse(typeof(Architecture), value, true),
                "Architecture of Dockerfiles to operate on (default is current OS architecture)");

            return architecture;
        }

        protected static void DefineManifestFilterOptions(ArgumentSyntax syntax, IManifestFilterOptions filterOptions)
        {
            filterOptions.Architecture = DefineArchitectureOption(syntax);

            string osVersion = null;
            syntax.DefineOption(
                "os-version",
                ref osVersion,
                "OS version of the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)");
            filterOptions.OsVersion = osVersion;

            IReadOnlyList<string> paths = Array.Empty<string>();
            syntax.DefineOptionList(
                "path",
                ref paths,
                "Directory paths containing the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)");
            filterOptions.Paths = paths;
        }

        public virtual ManifestFilter GetManifestFilter()
        {
            ManifestFilter filter = new ManifestFilter()
            {
                IncludeRepo = Repo,
            };

            if (this is IManifestFilterOptions)
            {
                IManifestFilterOptions filterOptions = (IManifestFilterOptions)this;
                filter.DockerArchitecture = filterOptions.Architecture;
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
            ArgumentCommand command = syntax.DefineCommand(CommandName, this);
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
