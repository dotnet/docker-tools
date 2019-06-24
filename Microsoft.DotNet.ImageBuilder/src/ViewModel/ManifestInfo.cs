// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ManifestInfo
    {
        /// <summary>
        /// All of the repos that are defined in the manifest.
        /// </summary>
        public IEnumerable<RepoInfo> AllRepos { get; private set; }

        /// <summary>
        /// The subet of manifest repos after applying the command line filter options.
        /// </summary>
        public IEnumerable<RepoInfo> FilteredRepos { get; private set; }

        public Manifest Model { get; private set; }
        public string Registry { get; private set; }
        public VariableHelper VariableHelper { get; set; }

        private ManifestInfo()
        {
        }

        public static ManifestInfo Load(IManifestOptionsInfo options)
        {
            Logger.WriteHeading("READING MANIFEST");

            ManifestInfo manifest = ManifestInfo.Create(
                options.Manifest,
                options.GetManifestFilter(),
                options);

            if (options.IsVerbose)
            {
                Logger.WriteMessage(JsonConvert.SerializeObject(manifest, Formatting.Indented));
            }

            return manifest;
        }

        private static ManifestInfo Create(string manifestPath, ManifestFilter manifestFilter, IManifestOptionsInfo options)
        {
            string baseDirectory = Path.GetDirectoryName(manifestPath);
            string manifestJson = File.ReadAllText(manifestPath);
            Manifest model = JsonConvert.DeserializeObject<Manifest>(manifestJson);
            model.Validate();

            ManifestInfo manifestInfo = new ManifestInfo();
            manifestInfo.Model = model;
            manifestInfo.Registry = options.RegistryOverride ?? model.Registry;
            manifestInfo.VariableHelper = new VariableHelper(model, options, manifestInfo.GetTagById, manifestInfo.GetRepoById);
            manifestInfo.AllRepos = manifestInfo.Model.Repos
                .Select(repo => RepoInfo.Create(
                    repo,
                    manifestInfo.Registry,
                    model.Registry,
                    manifestFilter,
                    options,
                    manifestInfo.VariableHelper,
                    baseDirectory))
                .ToArray();

            IEnumerable<string> repoNames = manifestInfo.AllRepos.Select(repo => repo.Name).ToArray();
            foreach (PlatformInfo platform in manifestInfo.GetAllPlatforms())
            {
                platform.Initialize(repoNames, manifestInfo.Registry);
            }

            IEnumerable<Repo> filteredRepoModels = manifestFilter.GetRepos(manifestInfo.Model);
            manifestInfo.FilteredRepos = manifestInfo.AllRepos
                .Where(repo => filteredRepoModels.Contains(repo.Model))
                .ToArray();

            return manifestInfo;
        }

        public IEnumerable<ImageInfo> GetAllImages() => AllRepos.SelectMany(repo => repo.AllImages);

        public IEnumerable<PlatformInfo> GetAllPlatforms() => GetAllImages().SelectMany(image => image.AllPlatforms);

        private IEnumerable<TagInfo> GetAllTags()
        {
            IEnumerable<ImageInfo> images = GetAllImages()
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

        public RepoInfo GetFilteredRepoById(string id)
        {
            return FilteredRepos.FirstOrDefault(repo => repo.Id == id);
        }

        public PlatformInfo GetPlatformByTag(string fullTagName)
        {
            PlatformInfo result = AllRepos
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
