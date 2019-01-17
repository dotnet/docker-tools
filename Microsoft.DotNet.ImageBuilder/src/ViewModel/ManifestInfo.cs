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
        public IEnumerable<ImageInfo> ActiveImages { get; private set; }
        private ManifestFilter ManifestFilter { get; set; }
        public Manifest Model { get; private set; }
        public IEnumerable<RepoInfo> Repos { get; private set; }
        public VariableHelper VariableHelper { get; set; }

        private ManifestInfo()
        {
        }

        public static ManifestInfo Create(Manifest model, ManifestFilter manifestFilter, IOptionsInfo options)
        {
            ManifestInfo manifestInfo = new ManifestInfo();
            manifestInfo.Model = model;
            manifestInfo.ManifestFilter = manifestFilter;
            manifestInfo.VariableHelper = new VariableHelper(model, options, manifestInfo.GetTagById, manifestInfo.GetRepoById);
            string registryName = options.RegistryOverride ?? model.Registry;
            manifestInfo.Repos = manifestFilter.GetRepos(manifestInfo.Model)
                .Select(repo => RepoInfo.Create(repo, registryName, manifestFilter, options, manifestInfo.VariableHelper))
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
                .SelectMany(platform => platform.ExternalFromImages)
                .Distinct();
        }

        public PlatformInfo GetPlatformByTag(string fullTagName)
        {
            PlatformInfo result = this.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(image => image.Platforms)
                .FirstOrDefault(platform => platform.Tags.Any(tag => tag.FullyQualifiedName == fullTagName));

            if (result == null)
            {
                throw new InvalidOperationException($"Unable to find platform for the tag '{fullTagName}'");
            }

            return result;
        }

        public RepoInfo GetRepoById(string id)
        {
            return Repos.FirstOrDefault(repo => repo.Id == id);
        }

        public TagInfo GetTagById(string id)
        {
            return GetAllTags()
                .FirstOrDefault(kvp => kvp.Model.Id == id);
        }
    }
}
