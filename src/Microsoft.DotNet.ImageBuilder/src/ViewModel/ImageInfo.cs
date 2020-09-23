// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
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
        public string ProductVersion { get; private set; }

        private ImageInfo()
        {
        }

        public static ImageInfo Create(
            Image model, string fullRepoModelName, string repoName, ManifestFilter manifestFilter, VariableHelper variableHelper, string baseDirectory)
        {
            ImageInfo imageInfo = new ImageInfo
            {
                Model = model,
                ProductVersion = variableHelper.SubstituteValues(model.ProductVersion)
            };

            if (model.SharedTags == null)
            {
                imageInfo.SharedTags = Enumerable.Empty<TagInfo>();
            }
            else
            {
                imageInfo.SharedTags = model.SharedTags
                    .Select(kvp => TagInfo.Create(kvp.Key, kvp.Value, repoName, variableHelper))
                    .ToArray();
            }

            imageInfo.AllPlatforms = model.Platforms
                .Select(platform => PlatformInfo.Create(platform, fullRepoModelName, repoName, variableHelper, baseDirectory))
                .ToArray();

            IEnumerable<Platform> filteredPlatformModels = manifestFilter.GetPlatforms(model);
            imageInfo.FilteredPlatforms = imageInfo.AllPlatforms
                .Where(platform => filteredPlatformModels.Contains(platform.Model));

            return imageInfo;
        }
    }
}
