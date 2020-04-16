// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetStaleImagesOptions : Options, IFilterableOptions, IGitOptionsHost
    {
        protected override string CommandHelp => "Gets paths to images whose base images are out-of-date";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public GitOptions GitOptions { get; } = new GitOptions();

        public string SubscriptionsPath { get; set; }
        public string VariableName { get; set; }

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

            FilterOptions.DefineOptions(syntax);
            GitOptions.DefineOptions(syntax);
        }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            GitOptions.DefineParameters(syntax);

            string variableName = null;
            syntax.DefineParameter(
                "image-paths-variable",
                ref variableName,
                "The Azure Pipeline variable name to assign the image paths to");
            VariableName = variableName;
        }
    }
}
