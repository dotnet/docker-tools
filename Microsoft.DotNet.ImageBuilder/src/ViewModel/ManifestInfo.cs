// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestInfo
    {
        public IEnumerable<RepoInfo> AllRepos { get; private set; }
        public IEnumerable<RepoInfo> FilteredRepos { get; private set; }
        private ManifestFilter ManifestFilter { get; set; }
        public Manifest Model { get; private set; }
        public string Registry { get; private set; }
        public VariableHelper VariableHelper { get; set; }

        private ManifestInfo()
        {
        }

        public static ManifestInfo Create(Manifest model, ManifestFilter manifestFilter, IOptionsInfo options)
        {
            ManifestInfo manifestInfo = new ManifestInfo();
            manifestInfo.Model = model;
            manifestInfo.ManifestFilter = manifestFilter;
            manifestInfo.Registry = options.RegistryOverride ?? model.Registry;
            manifestInfo.VariableHelper = new VariableHelper(model, options, manifestInfo.GetTagById, manifestInfo.GetRepoById);
            manifestInfo.AllRepos = manifestInfo.Model.Repos
                .Select(repo => RepoInfo.Create(repo, manifestInfo.Registry, manifestFilter, options, manifestInfo.VariableHelper))
                .ToArray();

            IEnumerable<string> repoNames = manifestInfo.AllRepos.Select(repo => repo.Name).ToArray();
            foreach (PlatformInfo platform in manifestInfo.AllRepos.SelectMany(repo => repo.AllImages).SelectMany(image => image.AllPlatforms))
            {
                platform.Initialize(repoNames);
            }

            IEnumerable<Repo> filteredRepoModels = manifestFilter.GetRepos(manifestInfo.Model);
            manifestInfo.FilteredRepos = manifestInfo.AllRepos
                .Where(repo => filteredRepoModels.Contains(repo.Model))
                .ToArray();

            return manifestInfo;
        }

        private IEnumerable<TagInfo> GetAllTags()
        {
            IEnumerable<ImageInfo> images = AllRepos
                .SelectMany(repo => repo.AllImages)
                .ToArray();
            IEnumerable<TagInfo> sharedTags = images
                .SelectMany(image => image.SharedTags);
            IEnumerable<TagInfo> platformTags = images
                .SelectMany(image => image.AllPlatforms)
                .SelectMany(platform => platform.Tags);
            return sharedTags
                .Concat(platformTags);
        }

        public IEnumerable<string> GetExternalFromImages()
        {
            return GetFilteredImages()
                .SelectMany(image => image.FilteredPlatforms)
                .SelectMany(platform => platform.ExternalFromImages)
                .Distinct();
        }

        public IEnumerable<ImageInfo> GetFilteredImages()
        {
            return FilteredRepos
                .SelectMany(repo => repo.FilteredImages);
        }

        public IEnumerable<PlatformInfo> GetFilteredPlatforms()
        {
            return GetFilteredImages()
                .SelectMany(image => image.FilteredPlatforms);
        }

        public IEnumerable<TagInfo> GetFilteredPlatformTags()
        {
            return GetFilteredPlatforms()
                .SelectMany(platform => platform.Tags);
        }

        public PlatformInfo GetPlatformByTag(string fullTagName)
        {
            PlatformInfo result = this.AllRepos
                .SelectMany(repo => repo.AllImages)
                .SelectMany(image => image.AllPlatforms)
                .FirstOrDefault(platform => platform.Tags.Any(tag => tag.FullyQualifiedName == fullTagName));

            if (result == null)
            {
                throw new InvalidOperationException($"Unable to find platform for the tag '{fullTagName}'");
            }

            return result;
        }

        public RepoInfo GetRepoById(string id)
        {
            return AllRepos.FirstOrDefault(repo => repo.Id == id);
        }

        public TagInfo GetTagById(string id)
        {
            return GetAllTags()
                .FirstOrDefault(kvp => kvp.Model.Id == id);
        }
    }
}
