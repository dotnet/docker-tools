// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class CustomBuildLegGroupingInfo
    {
        public CustomBuildLegGroupingInfo(string name, string[] dependencyImages)
        {
            this.Name = name;
            this.DependencyImages = dependencyImages;
        }

        public string Name { get; }

        public string[] DependencyImages { get; }
    }
}
