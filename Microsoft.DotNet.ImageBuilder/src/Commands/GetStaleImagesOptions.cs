// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetStaleImagesOptions : Options, IFilterableOptions
    {
        protected override string CommandHelp => "Gets paths to images whose base images are out-of-date";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public string SubscriptionsPath { get; set; }
        public string ImageInfoPath { get; set; }
        public string VariableName { get; set; }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            FilterOptions.ParseCommandLine(syntax);

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

            string variableName = null;
            syntax.DefineParameter(
                "image-paths-variable",
                ref variableName,
                "The Azure Pipeline variable name to assign the image paths to");
            VariableName = variableName;
        }
    }
}
