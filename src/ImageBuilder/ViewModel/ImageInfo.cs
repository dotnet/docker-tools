// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.DockerTools.ImageBuilder.Models.Manifest;


#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.ViewModel
{
    public class ImageInfo
    {
        /// <summary>
        /// All of the platforms that are defined in the manifest for this image.
        /// </summary>
        public IEnumerable<PlatformInfo> AllPlatforms { get; set; }

        /// <summary>
        /// The subet of image platforms after applying the command line filter options.
        /// </summary>
        public IEnumerable<PlatformInfo> FilteredPlatforms { get; private set; }

        public Image Model { get; private set; }
        public IEnumerable<TagInfo> SharedTags { get; private set; }
        public string? ProductVersion { get; private set; }

        private ImageInfo(Image model, string? productVersion, IEnumerable<TagInfo> sharedTags, IEnumerable<PlatformInfo> allPlatforms,
            IEnumerable<PlatformInfo> filteredPlatforms)
        {
            Model = model;
            ProductVersion = productVersion;
            SharedTags = sharedTags;
            AllPlatforms = allPlatforms;
            FilteredPlatforms = filteredPlatforms;
        }

        public static ImageInfo Create(
            Image model, string fullRepoModelName, string repoName, ManifestFilter manifestFilter, VariableHelper variableHelper, string baseDirectory)
        {
            IEnumerable<TagInfo> sharedTags;
            if (model.SharedTags == null)
            {
                sharedTags = Enumerable.Empty<TagInfo>();
            }
            else
            {
                sharedTags = model.SharedTags
                    .Select(kvp => TagInfo.Create(kvp.Key, kvp.Value, repoName, variableHelper))
                    .ToArray();
            }

            IEnumerable<PlatformInfo> allPlatforms = model.Platforms
                .Select(platform => PlatformInfo.Create(platform, fullRepoModelName, repoName, variableHelper, baseDirectory))
                .ToArray();

            string? productVersion = variableHelper.SubstituteValues(model.ProductVersion);

            IEnumerable<Platform> filteredPlatformModels = manifestFilter.FilterPlatforms(model.Platforms, productVersion);
            IEnumerable<PlatformInfo> filteredPlatforms = allPlatforms
                .Where(platform => filteredPlatformModels.Contains(platform.Model));

            return new ImageInfo(
                model,
                productVersion,
                sharedTags,
                allPlatforms,
                filteredPlatforms);
        }
    }
}
#nullable disable
