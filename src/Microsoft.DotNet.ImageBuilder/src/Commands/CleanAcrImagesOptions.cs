// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CleanAcrImagesOptions : Options
    {
        protected override string CommandHelp => "Removes unnecessary images from an ACR";

        public string RepoName { get; set; }
        public CleanAcrImagesAction Action { get; set; }
        public int Age { get; set; }
        public ServicePrincipalOptions ServicePrincipalOptions { get; } = new ServicePrincipalOptions();
        public string Subscription { get; set; }
        public string ResourceGroup { get; set; }
        public string RegistryName { get; set; }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string repoName = null;
            syntax.DefineParameter("repo", ref repoName, "Name of repo to target (wildcard chars * and ? supported)");
            RepoName = repoName;

            ServicePrincipalOptions.DefineParameters(syntax);

            string subscription = null;
            syntax.DefineParameter("subscription", ref subscription, "Azure subscription to operate on");
            Subscription = subscription;

            string resourceGroup = null;
            syntax.DefineParameter("resource-group", ref resourceGroup, "Azure resource group to operate on");
            ResourceGroup = resourceGroup;

            string registryName = null;
            syntax.DefineParameter("registry", ref registryName, "Name of the registry");
            RegistryName = registryName;
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            CleanAcrImagesAction action = CleanAcrImagesAction.PruneDangling;
            syntax.DefineOption("action", ref action,
                value => (CleanAcrImagesAction)Enum.Parse(typeof(CleanAcrImagesAction), value, true),
                $"Type of delete action. {EnumHelper.GetHelpTextOptions(action)}");
            Action = action;

            int age = 30;
            syntax.DefineOption("age", ref age, $"Minimum age (days) of repo or images to be deleted (default: {age})");
            Age = age;
        }
    }

    public enum CleanAcrImagesAction
    {
        /// <summary>
        /// Deletes untagged images in a repo.
        /// </summary>
        PruneDangling,

        /// <summary>
        /// Deletes all images in a repo.
        /// </summary>
        PruneAll,

        /// <summary>
        /// Deletes the repo.
        /// </summary>
        Delete
    }
}
