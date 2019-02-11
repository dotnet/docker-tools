// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildOptions : DockerRegistryOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Builds and Tests Dockerfiles";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();
        public bool IsPushEnabled { get; set; }
        public bool IsRetryEnabled { get; set; }
        public bool IsSkipPullingEnabled { get; set; }

        public BuildOptions() : base()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            FilterOptions.ParseCommandLine(syntax);

            bool isPushEnabled = false;
            syntax.DefineOption("push", ref isPushEnabled, "Push built images to Docker registry");
            IsPushEnabled = isPushEnabled;

            bool isRetryEnabled = false;
            syntax.DefineOption("retry", ref isRetryEnabled, "Retry building images upon failure");
            IsRetryEnabled = isRetryEnabled;

            bool isSkipPullingEnabled = false;
            syntax.DefineOption("skip-pulling", ref isSkipPullingEnabled, "Skip explicitly pulling the base images of the Dockerfiles");
            IsSkipPullingEnabled = isSkipPullingEnabled;
        }
    }
}
