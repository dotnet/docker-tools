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
        private ManifestFilter ManifestFilter { get; set; }
        public Manifest Model { get; private set; }
        public IEnumerable<RepoInfo> Repos { get; private set; }
        private VariableHelper VariableHelper { get; set; }

        private ManifestInfo()
        {
        }

        public static ManifestInfo Create(
            Manifest model, ManifestFilter manifestFilter, string repoOwner, IDictionary<string, string> optionVariables)
        {
            ManifestInfo manifestInfo = new ManifestInfo();
            manifestInfo.Model = model;
            manifestInfo.ManifestFilter = manifestFilter;
            manifestInfo.VariableHelper = new VariableHelper(model, optionVariables, manifestInfo.GetTagById);
            manifestInfo.Repos = manifestFilter.GetRepos(manifestInfo.Model)
                .Select(repo => RepoInfo.Create(repo, manifestFilter, repoOwner, manifestInfo.VariableHelper))
                .ToArray();
            manifestInfo.ActiveImages = manifestInfo.Repos
                .SelectMany(repo => repo.Images)
                .Where(image => image.ActivePlatforms.Any())
                .ToArray();

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

        public TagInfo GetTagById(string id)
        {
            return GetAllTags()
                .Single(kvp => kvp.Model.Id == id);
        }

        public IEnumerable<string> GetTestCommands()
        {
            return ManifestFilter.GetTestCommands(Model)
                .Select(command => VariableHelper.SubstituteValues(command));
        }

        public bool IsExternalImage(string image)
        {
            return Repos.All(repo => repo.IsExternalImage(image));
        }
    }
}
