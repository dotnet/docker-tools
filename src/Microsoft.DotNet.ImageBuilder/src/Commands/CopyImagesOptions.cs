// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class CopyImagesOptions : ManifestOptions
    {
        public string ResourceGroup { get; set; }
        public string Subscription { get; set; }

        public ServicePrincipalOptions ServicePrincipal { get; set; } = new ServicePrincipalOptions();

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            ServicePrincipal.DefineParameters(syntax);

            string subscription = null;
            syntax.DefineParameter("subscription", ref subscription, "Azure subscription to operate on");
            Subscription = subscription;

            string resourceGroup = null;
            syntax.DefineParameter("resource-group", ref resourceGroup, "Azure resource group to operate on");
            ResourceGroup = resourceGroup;
        }
    }
}
