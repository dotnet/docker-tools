// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class QueueBuildOptions : Options
    {
        protected override string CommandHelp => "Queues builds to update images";

        public string SubscriptionsPath { get; set; }
        public string BuildPersonalAccessToken { get; set; }
        public string BuildOrganization { get; set; }
        public string BuildProject { get; set; }
        public IEnumerable<string> AllSubscriptionImagePaths { get; set; }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            const string DefaultSubscriptionsPath = "subscriptions.json";
            string subscriptionsPath = DefaultSubscriptionsPath;
            syntax.DefineOption(
                "subscriptions-path",
                ref subscriptionsPath,
                $"Path to the subscriptions file (defaults to '{DefaultSubscriptionsPath}').");
            SubscriptionsPath = subscriptionsPath;

            IReadOnlyList<string> allSubscriptionImagePaths = Array.Empty<string>();
            syntax.DefineOptionList(
                "image-paths",
                ref allSubscriptionImagePaths,
                "JSON string mapping a subscription ID to the image paths to be built (from the output variable of getStaleImages)");
            AllSubscriptionImagePaths = allSubscriptionImagePaths;
        }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string buildPersonalAccessToken = null;
            syntax.DefineParameter(
                "build-pat",
                ref buildPersonalAccessToken,
                "Azure DevOps PAT for queuing builds");
            BuildPersonalAccessToken = buildPersonalAccessToken;

            string buildOrganization = null;
            syntax.DefineParameter(
                "build-organization",
                ref buildOrganization,
                "Azure DevOps organization for queuing builds");
            BuildOrganization = buildOrganization;

            string buildProject = null;
            syntax.DefineParameter(
                "build-project",
                ref buildProject,
                "Azure DevOps project for queuing builds");
            BuildProject = buildProject;
        }
    }
}
