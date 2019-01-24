// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ImageInfo
    {
        public IEnumerable<PlatformInfo> AllPlatforms { get; set; }
        public IEnumerable<PlatformInfo> FilteredPlatforms { get; private set; }
        public Image Model { get; private set; }
        public IEnumerable<TagInfo> SharedTags { get; private set; }

        private ImageInfo()
        {
        }

        public static ImageInfo Create(
            Image model, Repo repoModel, string repoName, ManifestFilter manifestFilter, VariableHelper variableHelper)
        {
            ImageInfo imageInfo = new ImageInfo();
            imageInfo.Model = model;

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

            imageInfo.AllPlatforms = manifestFilter.GetPlatforms(model)
                .Select(platform => PlatformInfo.Create(platform, repoModel, repoName, variableHelper))
                .ToArray();

            IEnumerable<Platform> filteredPlatformModels = manifestFilter.GetPlatforms(model);
            imageInfo.FilteredPlatforms = imageInfo.AllPlatforms
                .Where(platform => filteredPlatformModels.Contains(platform.Model));

            return imageInfo;
        }
    }
}
