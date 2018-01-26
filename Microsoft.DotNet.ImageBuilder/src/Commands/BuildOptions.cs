// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildOptions : DockerRegistryOptions
    {
        protected override string CommandHelp => "Builds and Tests Dockerfiles";
        protected override string CommandName => "build";

        public Architecture Architecture { get; set; }
        public bool IsPushEnabled { get; set; }
        public bool IsSkipPullingEnabled { get; set; }
        public bool IsTestRunDisabled { get; set; }
        public string OsVersion { get; set; }
        public string Path { get; set; }

        public BuildOptions() : base()
        {
        }

        public override ManifestFilter GetManifestFilter()
        {
            ManifestFilter filterInfo = base.GetManifestFilter();
            filterInfo.DockerArchitecture = Architecture;
            filterInfo.IncludeOsVersion = OsVersion;
            filterInfo.IncludePath = Path;

            return filterInfo;
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            Architecture = DefineArchitectureOption(syntax);

            string osVersion = null;
            syntax.DefineOption(
                "os-version",
                ref osVersion,
                "OS version of the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)");
            OsVersion = osVersion;

            string path = null;
            syntax.DefineOption(
                "path",
                ref path,
                "Directory path containing the Dockerfiles to build - wildcard chars * and ? supported (default is to build all)");
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
        }
    }
}
