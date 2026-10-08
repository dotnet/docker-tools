#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class TagInfo
    {
        private string BuildContextPath { get; set; }
        public string FullyQualifiedName { get; private set; }
        public Tag Model { get; private set; }
        public string Name { get; private set; }

        private TagInfo()
        {
        }

        public static TagInfo Create(
            string name,
            Tag model,
            string repoName,
            VariableHelper variableHelper,
            string buildContextPath = null)
        {
            var tagInfo = new TagInfo()
            {
                Model = model,
                BuildContextPath = buildContextPath,
                Name = variableHelper.SubstituteValues(name),
            };

            tagInfo.FullyQualifiedName = GetFullyQualifiedName(repoName, tagInfo.Name);

            return tagInfo;
        }

        public static string GetFullyQualifiedName(string repoName, string tagName)
        {
            return $"{repoName}:{tagName}";
        }
    }
}
