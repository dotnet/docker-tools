// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ImageInfoOptions : ManifestOptions
    {
        public string ImageInfoPath { get; set; }

        protected ImageInfoOptions()
        {
        }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            string imageInfoPath = null;
            syntax.DefineParameter("image-info-path", ref imageInfoPath, "Image info file path");
            ImageInfoPath = imageInfoPath;
        }
    }
}
