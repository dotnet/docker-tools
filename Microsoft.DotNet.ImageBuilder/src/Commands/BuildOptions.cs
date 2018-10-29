// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildOptions : DockerRegistryOptions, IManifestFilterOptions
    {
        protected override string CommandHelp => "Builds and Tests Dockerfiles";
        protected override string CommandName => "build";

        public Architecture Architecture { get; set; }
        public bool IsPushEnabled { get; set; }
        public bool IsSkipPullingEnabled { get; set; }
        public string OsVersion { get; set; }
        public IEnumerable<string> Paths { get; set; }

        public BuildOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            DefineManifestFilterOptions(syntax, this);

            bool isPushEnabled = false;
            syntax.DefineOption("push", ref isPushEnabled, "Push built images to Docker registry");
            IsPushEnabled = isPushEnabled;

            bool isSkipPullingEnabled = false;
            syntax.DefineOption("skip-pulling", ref isSkipPullingEnabled, "Skip explicitly pulling the base images of the Dockerfiles");
            IsSkipPullingEnabled = isSkipPullingEnabled;
        }
    }
}
