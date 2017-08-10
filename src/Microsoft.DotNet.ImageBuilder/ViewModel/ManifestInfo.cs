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
        public IEnumerable<string> ActivePlatformFullyQualifiedTags { get; private set; }
        public IEnumerable<ImageInfo> Images { get; private set; }
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
            manifestInfo.Images = manifestInfo.Repos
                .SelectMany(repo => repo.Images)
                .ToArray();
            manifestInfo.ActiveImages = manifestInfo.Images
                .Where(image => image.ActivePlatform != null)
                .ToArray();
            manifestInfo.ActivePlatformFullyQualifiedTags = manifestInfo.ActiveImages
                .SelectMany(image => image.ActivePlatform.Tags)
                .Select(tag => tag.FullyQualifiedName)
                .ToArray();
            manifestInfo.TestCommands = manifestFilter.GetTestCommands(manifestInfo.Model);

            return manifestInfo;
        }

        public IEnumerable<string> GetExternalFromImages()
        {
            return ActiveImages
                .SelectMany(image => image.ActivePlatform.FromImages)
                .Where(IsExternalImage)
                .Distinct();
        }

        public bool IsExternalImage(string image)
        {
            return Repos.All(repo => repo.IsExternalImage(image));
        }
    }
}
