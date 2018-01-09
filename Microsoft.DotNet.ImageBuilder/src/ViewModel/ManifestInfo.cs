// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestInfo
    {
        public IEnumerable<ImageInfo> ActiveImages { get; private set; }
        public Manifest Model { get; private set; }
        public IEnumerable<RepoInfo> Repos { get; private set; }
        public IEnumerable<string> TestCommands { get; private set; }

        private ManifestInfo()
        {
        }

        public static ManifestInfo Create(Manifest model, ManifestFilter manifestFilter, string repoOwner)
        {
            ManifestInfo manifestInfo = new ManifestInfo();
            manifestInfo.Model = model;
            manifestInfo.Repos = manifestFilter.GetRepos(manifestInfo.Model)
                .Select(repo => RepoInfo.Create(repo, manifestInfo.Model, manifestFilter, repoOwner))
                .ToArray();
            manifestInfo.ActiveImages = manifestInfo.Repos
                .SelectMany(repo => repo.Images)
                .Where(image => image.ActivePlatforms.Any())
                .ToArray();
            manifestInfo.TestCommands = manifestFilter.GetTestCommands(manifestInfo.Model);

            return manifestInfo;
        }

        private IEnumerable<TagInfo> GetAllTags()
        {
            IEnumerable<ImageInfo> images = Repos
                .SelectMany(repo => repo.Images)
                .ToArray();
            IEnumerable<TagInfo> sharedTags = images
                .SelectMany(image => image.SharedTags);
            IEnumerable<TagInfo> platformTags = images
                .SelectMany(image => image.Platforms)
                .SelectMany(platform => platform.Tags);
            return sharedTags
                .Concat(platformTags)
                .ToArray();
        }

        public IEnumerable<string> GetExternalFromImages()
        {
            return ActiveImages
                .SelectMany(image => image.ActivePlatforms)
                .SelectMany(platform => platform.FromImages)
                .Where(IsExternalImage)
                .Distinct();
        }

        public string GetReferenceVariableValue(string variableName)
        {
            const string tagRef = "TagRef:";

            string variableValue = null;
            if (variableName.StartsWith(tagRef))
            {
                string tagId = variableName.Substring(tagRef.Length);
                variableValue = GetAllTags()
                    .Single(kvp => kvp.Model.Id == tagId).Name;
            }

            return variableValue;
        }

        public bool IsExternalImage(string image)
        {
            return Repos.All(repo => repo.IsExternalImage(image));
        }
    }
}
