// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;

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

        public static TagInfo Create(string name, Tag model, Manifest manifest, string repoName, string filePath = "")
        {
            TagInfo tagInfo = new TagInfo();
            tagInfo.Model = model;
            Func<string, string> GetSha = Utilities.GetSha(filePath);
            tagInfo.Name = Utilities.SubstituteVariables(
                variables: manifest.TagVariables, expression: name, getVariableValue: GetSha);
            tagInfo.FullyQualifiedName = $"{repoName}:{tagInfo.Name}";

            return tagInfo;
        }
    }
}
