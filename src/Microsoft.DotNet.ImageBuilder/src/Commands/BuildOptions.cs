// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildOptions : DockerRegistryOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Builds Dockerfiles";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();
        public bool IsPushEnabled { get; set; }
        public bool IsRetryEnabled { get; set; }
        public bool IsSkipPullingEnabled { get; set; }
        public string ImageInfoOutputPath { get; set; }
        public string ImageInfoSourcePath { get; set; }
        public string SourceRepoUrl { get; set; }
        public bool DisableCaching { get; set; }

        public BuildOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);

            bool isPushEnabled = false;
            syntax.DefineOption("push", ref isPushEnabled, "Push built images to Docker registry");
            IsPushEnabled = isPushEnabled;

            bool isRetryEnabled = false;
            syntax.DefineOption("retry", ref isRetryEnabled, "Retry building images upon failure");
            IsRetryEnabled = isRetryEnabled;

            bool isSkipPullingEnabled = false;
            syntax.DefineOption("skip-pulling", ref isSkipPullingEnabled, "Skip explicitly pulling the base images of the Dockerfiles");
            IsSkipPullingEnabled = isSkipPullingEnabled;

            string imageInfoOutputPath = null;
            syntax.DefineOption("image-info-output-path", ref imageInfoOutputPath, "Path to output image info");
            ImageInfoOutputPath = imageInfoOutputPath;

            string imageInfoSourcePath = null;
            syntax.DefineOption("image-info-source-path", ref imageInfoSourcePath, "Path to source image info");
            ImageInfoSourcePath = imageInfoSourcePath;

            string sourceRepoUrl = null;
            syntax.DefineOption("source-repo", ref sourceRepoUrl, "Repo URL of the Dockerfile sources");
            SourceRepoUrl = sourceRepoUrl;

            bool disableCaching = false;
            syntax.DefineOption("disable-caching", ref disableCaching, "Disables build cache feature");
            DisableCaching = disableCaching;
        }
    }
}
