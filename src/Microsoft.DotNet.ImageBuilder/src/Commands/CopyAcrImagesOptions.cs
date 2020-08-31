// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyAcrImagesOptions : CopyImagesOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Copies the platform images as specified in the manifest between repositories of an ACR";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();
        
        public string SourceRepoPrefix { get; set; }
        
        public string ImageInfoPath { get; set; }

        public CopyAcrImagesOptions() : base()
        {
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);

            string imageInfoPath = null;
            syntax.DefineOption("image-info", ref imageInfoPath, "Path to image info file");
            ImageInfoPath = imageInfoPath;
        }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string sourceRepoPrefix = null;
            syntax.DefineParameter("source-repo-prefix", ref sourceRepoPrefix, "Prefix of the source ACR repository to copy images from");
            SourceRepoPrefix = sourceRepoPrefix;
        }
    }
}
