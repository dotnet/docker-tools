#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    /// <summary>
    /// Parsed and validated view model of a manifest JSON file. Provides query methods
    /// for repos, images, platforms, and their dependency graphs.
    /// </summary>
    /// <remarks>
    /// Instances are created by <see cref="IManifestJsonService"/>. This class is a pure
    /// ViewModel — it contains no file I/O or static factory methods.
    /// </remarks>
    public class ManifestInfo
    {
        /// <summary>
        /// All of the repos that are defined in the manifest.
        /// </summary>
        public IEnumerable<RepoInfo> AllRepos { get; internal set; }

        /// <summary>
        /// The subset of manifest repos after applying the command line filter options.
        /// </summary>
        public IEnumerable<RepoInfo> FilteredRepos { get; internal set; }

        /// <summary>
        /// The deserialized manifest model.
        /// </summary>
        public Manifest Model { get; internal set; }

        /// <summary>
        /// The effective Docker registry, accounting for any command-line override.
        /// </summary>
        public string Registry { get; internal set; }

        /// <summary>
        /// Helper for resolving manifest variable substitutions.
        /// </summary>
        public VariableHelper VariableHelper { get; internal set; }

        /// <summary>
        /// Gets the directory of the manifest file.
        /// </summary>
        public string Directory { get; internal set; }

        /// <summary>
        /// Path to the manifest's readme file, if defined.
        /// </summary>
        public string ReadmePath { get; internal set; }

        /// <summary>
        /// Path to the manifest's readme template file, if defined.
        /// </summary>
        public string ReadmeTemplatePath { get; internal set; }

        internal ManifestInfo()
        {
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

        /// <summary>
        /// Returns the set of ancestors that the platform is dependent upon. This recursively walks the parent hierarchy.
        /// </summary>
        public IEnumerable<PlatformInfo> GetAncestors(PlatformInfo platform, IEnumerable<PlatformInfo> availablePlatforms) =>
            GetParents(platform, availablePlatforms)
                .SelectMany(parent => new PlatformInfo[] { parent }.Concat(GetAncestors(parent, availablePlatforms)));

        /// <summary>
        /// Returns the set of parents that the platform is directly dependent upon. Not recursive.
        /// </summary>
        public IEnumerable<PlatformInfo> GetParents(PlatformInfo platform, IEnumerable<PlatformInfo> availablePlatforms) =>
            platform.InternalFromImages
                .Select(fromImage => GetPlatformByTag(fromImage))
                .Intersect(availablePlatforms);

        /// <summary>
        /// Returns the set of children are directly dependent upon the given platform. Not recursive.
        /// </summary>
        private static IEnumerable<PlatformInfo> GetChildren(PlatformInfo parent, IEnumerable<PlatformInfo> allPlatforms) =>
            allPlatforms
                .Where(platform =>
                    platform.InternalFromImages
                        .Intersect(parent.Tags.Select(tag => tag.FullyQualifiedName))
                        .Any());

        /// <summary>
        /// Gets the set of platforms which are descendants of the given platform.
        /// </summary>
        /// <param name="parent">The platform whose descendants are to be returned.</param>
        /// <param name="availablePlatforms">The set of available platforms to select from.</param>
        /// <param name="includeAncestorsOfDescendants">Indicates whether to recursively gets the ancestor graph of each
        /// descendant to account for the scenario where a descendant has more than one parent.</param>
        public IEnumerable<PlatformInfo> GetDescendants(
            PlatformInfo parent, IEnumerable<PlatformInfo> availablePlatforms, bool includeAncestorsOfDescendants = false)
        {
            List<PlatformInfo> platforms = new();
            GetDescendants(parent, availablePlatforms, includeAncestorsOfDescendants, platforms);

            // Remove the first item which is the the platform that was provided, we only want the descendants of that
            platforms.RemoveAt(0);

            return platforms;
        }

        private void GetDescendants(
            PlatformInfo parent, IEnumerable<PlatformInfo> availablePlatforms, bool includeAncestorsOfDescendants,
            List<PlatformInfo> platforms)
        {
            if (platforms.Contains(parent))
            {
                return;
            }

            platforms.Add(parent);

            foreach (PlatformInfo child in GetChildren(parent, availablePlatforms))
            {
                GetDescendants(child, availablePlatforms, includeAncestorsOfDescendants, platforms);
            }

            if (includeAncestorsOfDescendants)
            {
                foreach (PlatformInfo childParent in GetParents(parent, availablePlatforms))
                {
                    GetDescendants(childParent, availablePlatforms, includeAncestorsOfDescendants, platforms);
                }
            }
        }
    }
}
