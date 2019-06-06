// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class RebuildStaleImagesOptions : Options
    {
        protected override string CommandHelp => "Uses a subscriptions file to determine which images are using out-of-date base images and queues a build to update them.";

        public string SubscriptionsPath { get; set; }
        public string ImageInfoPath { get; set; }
        public string BuildPersonalAccessToken { get; set; }
        public string BuildOrganization { get; set; }
        public string BuildProject { get; set; }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            const string DefaultSubscriptionsPath = "subscriptions.json";
            string subscriptionsPath = DefaultSubscriptionsPath;
            syntax.DefineOption(
                "subscriptions-path",
                ref subscriptionsPath,
                $"Path to the subscriptions file (defaults to '{DefaultSubscriptionsPath}').");
            SubscriptionsPath = subscriptionsPath;

            const string DefaultImageInfoPath = "image-info.json";
            string imageDataPath = DefaultImageInfoPath;
            syntax.DefineOption(
                "image-info-path",
                ref imageDataPath,
                $"Path to the file containing image info (defaults to '{DefaultImageInfoPath}').");
            ImageInfoPath = imageDataPath;

            string buildPersonalAccessToken = null;
            syntax.DefineParameter(
                "build-pat",
                ref buildPersonalAccessToken,
                "The personal access token used to connect to Azure DevOps for queuing builds.");
            BuildPersonalAccessToken = buildPersonalAccessToken;

            string buildOrganization = null;
            syntax.DefineParameter(
                "build-organization",
                ref buildOrganization,
                "The name of the Azure DevOps organization where builds are queued.");
            BuildOrganization = buildOrganization;

            string buildProject = null;
            syntax.DefineParameter(
                "build-project",
                ref buildProject,
                "The name of the Azure DevOps project where builds are queued.");
            BuildProject = buildProject;
        }
    }
}
