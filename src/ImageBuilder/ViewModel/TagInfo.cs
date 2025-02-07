// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class TagInfo
    {
        private string BuildContextPath { get; set; }
        public string FullyQualifiedName { get; private set; }
        public Tag Model { get; private set; }
        public string Name { get; private set; }
        public string SyndicatedRepo { get; private set; }
        public string[] SyndicatedDestinationTags { get; private set; }

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
            TagInfo tagInfo = new TagInfo
            {
                Model = model,
                BuildContextPath = buildContextPath
            };
            tagInfo.Name = variableHelper.SubstituteValues(name);
            tagInfo.FullyQualifiedName = GetFullyQualifiedName(repoName, tagInfo.Name);

            if (model.Syndication != null)
            {
                tagInfo.SyndicatedRepo = variableHelper.SubstituteValues(model.Syndication.Repo);
                tagInfo.SyndicatedDestinationTags = model.Syndication.DestinationTags?
                    .Select(tag => variableHelper.SubstituteValues(tag))
                    .ToArray();
                if (tagInfo.SyndicatedDestinationTags is null || !tagInfo.SyndicatedDestinationTags.Any())
                {
                    tagInfo.SyndicatedDestinationTags = new string[] { tagInfo.Name };
                }
            }

            return tagInfo;
        }

        public static string GetFullyQualifiedName(string repoName, string tagName)
        {
            return $"{repoName}:{tagName}";
        }
    }
}
