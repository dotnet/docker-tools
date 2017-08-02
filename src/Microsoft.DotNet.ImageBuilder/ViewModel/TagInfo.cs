// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class TagInfo
    {
        public string FullyQualifiedName { get; private set; }
        public Tag Model { get; private set; }
        public string Name { get; private set; }

        private TagInfo()
        {
        }

        public static TagInfo Create(Tag model, Manifest manifest, string repoName)
        {
            TagInfo tagInfo = new TagInfo();
            tagInfo.Model = model;
            tagInfo.Name = manifest.SubstituteTagVariables(model.Name);
            tagInfo.FullyQualifiedName = $"{repoName}:{tagInfo.Name}";

            return tagInfo;
        }
    }
}
