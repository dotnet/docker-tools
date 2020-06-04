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
        public int DaysOld { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Tenant { get; set; }
        public string Subscription { get; set; }
        public string ResourceGroup { get; set; }
        public string RegistryName { get; set; }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string repoName = null;
            syntax.DefineParameter("repo", ref repoName, "Name of repo to target (wildcard chars * and ? supported)");
            RepoName = repoName;

            string username = null;
            syntax.DefineParameter("username", ref username, "The URL or name associated with the service principal to use");
            Username = username;

            string password = null;
            syntax.DefineParameter("password", ref password, "The service principal password or the X509 certificate to use");
            Password = password;

            string tenant = null;
            syntax.DefineParameter("tenant", ref tenant, "The tenant associated with the service principal to use");
            Tenant = tenant;

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

            const CleanAcrImagesAction defaultAction = CleanAcrImagesAction.PruneDangling;
            string action = defaultAction.ToString().ToCamelCase();
            syntax.DefineOption("action", ref action,
                $"Type of delete action. {EnumHelper.GetHelpTextOptions(defaultAction)}");
            Action = (CleanAcrImagesAction)Enum.Parse(typeof(CleanAcrImagesAction), action, ignoreCase: true);

            int daysOld = 30;
            syntax.DefineOption("days", ref action, "Number of days old to be considered for deletion");
            DaysOld = daysOld;
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
