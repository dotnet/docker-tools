// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        /// <summary>
        /// Gets the directory of the manifest file.
        /// </summary>
        public string Directory { get; private set; }
        public string ReadmePath { get; private set; }
        public string ReadmeTemplatePath { get; private set; }

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
            string manifestDirectory = PathHelper.GetNormalizedDirectory(manifestPath);
            Manifest model = LoadModel(manifestPath, manifestDirectory);
            model.Validate(manifestDirectory);

            ManifestInfo manifestInfo = new ManifestInfo
            {
                Model = model,
                Registry = options.RegistryOverride ?? model.Registry,
                Directory = manifestDirectory
            };
            manifestInfo.VariableHelper = new VariableHelper(model, options, manifestInfo.GetRepoById);
            manifestInfo.AllRepos = manifestInfo.Model.Repos
                .Select(repo => RepoInfo.Create(
                    repo,
                    manifestInfo.Registry,
                    model.Registry,
                    manifestFilter,
                    options,
                    manifestInfo.VariableHelper,
                    manifestInfo.Directory))
                .ToArray();

            if (model.Readme != null)
            {
                manifestInfo.ReadmePath = Path.Combine(manifestInfo.Directory, model.Readme);
            }
            if (model.ReadmeTemplate != null)
            {
                manifestInfo.ReadmeTemplatePath = Path.Combine(manifestInfo.Directory, model.ReadmeTemplate);
            }

            IEnumerable<string> repoNames = manifestInfo.AllRepos.Select(repo => repo.QualifiedName).ToArray();
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
        
        public ImageInfo GetImageByPlatform(PlatformInfo platform) =>
            GetAllImages()
                .FirstOrDefault(image => image.AllPlatforms.Contains(platform));

        public IEnumerable<PlatformInfo> GetAllPlatforms() => GetAllImages().SelectMany(image => image.AllPlatforms);

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

        public RepoInfo GetRepoByModelName(string name)
        {
            return AllRepos.FirstOrDefault(repo => repo.Model.Name == name);
        }

        public RepoInfo GetRepoByImage(ImageInfo image) =>
            AllRepos
                .FirstOrDefault(repoImage => repoImage.AllImages.Contains(image));

        private static Manifest LoadModel(string path, string manifestDirectory)
        {
            string manifestJson = File.ReadAllText(path);
            Manifest model = JsonConvert.DeserializeObject<Manifest>(manifestJson);

            if (model.Includes != null)
            {
                if (model.Variables == null)
                {
                    model.Variables = new Dictionary<string, string>();
                }

                foreach (string includePath in model.Includes)
                {
                    ModelExtensions.ValidateFileReference(includePath, manifestDirectory);
                    manifestJson = File.ReadAllText(Path.Combine(manifestDirectory, includePath));
                    Manifest includeModel = JsonConvert.DeserializeObject<Manifest>(manifestJson);
                    foreach (KeyValuePair<string, string> kvp in includeModel.Variables)
                    {
                        if (model.Variables.ContainsKey(kvp.Key))
                        {
                            throw new InvalidOperationException(
                                $"The manifest contains multiple '{kvp.Key}' variables.  Each variable name must be unique.");
                        }

                        model.Variables.Add(kvp.Key, kvp.Value);
                    }

                    model.Repos = model.Repos.Concat(includeModel.Repos).ToArray();

                    // Consolidate distinct repo instances that share the same name
                    model.Repos = model.Repos
                        .GroupBy(repo => repo.Name)
                        .Select(grouping => ConsolidateReposWithSameName(grouping))
                        .ToArray();
                }
            }

            return model;
        }

        private static Repo ConsolidateReposWithSameName(IGrouping<string, Repo> grouping)
        {
            // Validate that all repos which share the same name also don't have non-empty, conflicting values for the other settings
            IEnumerable<PropertyInfo> propertiesToValidate = typeof(Repo).GetProperties()
                .Where(prop => prop.Name != nameof(Repo.Images));
            foreach (PropertyInfo property in propertiesToValidate)
            {
                List<string> distinctNonEmptyPropertyValues = grouping
                    .Select(repo => property.GetValue(repo))
                    .Distinct()
                    .Select(item => item?.ToString())
                    .Where(val => !string.IsNullOrEmpty(val))
                    .Select(val => $"'{val}'")
                    .ToList();

                if (distinctNonEmptyPropertyValues.Count > 1)
                {
                    throw new InvalidOperationException(
                        "The manifest contains multiple repos with the same name that also do not have the same " +
                        $"value for the '{property.Name}' property. Distinct values: {string.Join(", ", distinctNonEmptyPropertyValues)}");
                }
            }

            // Create a new consolidated repo model instance that contains whichever non-empty state was set amongst the group of repos.
            // All of the images within the repos are combined together into a single set.
            return new Repo
            {
                Name = grouping.Key,
                Id = grouping
                    .Select(repo => repo.Id)
                    .FirstOrDefault(val => !string.IsNullOrEmpty(val)),
                McrTagsMetadataTemplate = grouping
                    .Select(repo => repo.McrTagsMetadataTemplate)
                    .FirstOrDefault(val => !string.IsNullOrEmpty(val)),
                Readme = grouping
                    .Select(repo => repo.Readme)
                    .FirstOrDefault(val => !string.IsNullOrEmpty(val)),
                ReadmeTemplate = grouping
                    .Select(repo => repo.ReadmeTemplate)
                    .FirstOrDefault(val => !string.IsNullOrEmpty(val)),
                Images = grouping
                    .SelectMany(repo => repo.Images)
                    .ToArray()
            };
        }
    }
}
