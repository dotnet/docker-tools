// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System.Collections.Generic;
using System.CommandLine;
using System.Collections.ObjectModel;
using System;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildOptions : Options
    {
        protected override string CommandHelp { get; } = "Builds and Tests Dockerfiles";
        protected override string CommandName { get; } = "build";

        public Architecture Architecture { get; set; }
        public bool IsPushEnabled { get; set; }
        public bool IsSkipPullingEnabled { get; set; }
        public bool IsTestRunDisabled { get; set; }
        public string Path { get; set; }
        public IDictionary<string, string> TestVariables { get; set; }

        public BuildOptions() : base()
        {
        }

        public override ManifestFilter GetManifestFilter()
        {
            ManifestFilter filterInfo = base.GetManifestFilter();
            filterInfo.DockerArchitecture = Architecture;
            filterInfo.IncludePath = Path;

            return filterInfo;
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            Architecture architecture = DockerHelper.GetArchitecture();
            syntax.DefineOption(
                "architecture",
                ref architecture,
                value => (Architecture)Enum.Parse(typeof(Architecture), value, true),
                "The architecture of the Docker images to build (default is the current OS architecture)");
            Architecture = architecture;

            string path = null;
            syntax.DefineOption("path", ref path, "Path of the directory to build (Default is to build all)");
            Path = path;

            bool isPushEnabled = false;
            syntax.DefineOption("push", ref isPushEnabled, "Push built images to Docker registry");
            IsPushEnabled = isPushEnabled;

            bool isSkipPullingEnabled = false;
            syntax.DefineOption("skip-pulling", ref isSkipPullingEnabled, "Skip explicitly pulling the base images of the Dockerfiles");
            IsSkipPullingEnabled = isSkipPullingEnabled;

            bool isTestRunDisabled = false;
            syntax.DefineOption("skip-test", ref isTestRunDisabled, "Skip running the tests");
            IsTestRunDisabled = isTestRunDisabled;

            IReadOnlyList<string> nameValuePairs = Array.Empty<string>();
            syntax.DefineOptionList("test-var", ref nameValuePairs, "Named variables to substitute into the test commands (name=value)");
            TestVariables = nameValuePairs
                .Select(pair => pair.Split(new char[] { '=' }, 2))
                .ToDictionary(split => split[0], split => split[1]);
        }
    }
}
