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
        public IEnumerable<PlatformInfo> ActivePlatforms { get; private set; }
        public Image Model { get; private set; }
        public IEnumerable<PlatformInfo> Platforms { get; set; }
        public IEnumerable<TagInfo> SharedTags { get; private set; }

        private ImageInfo()
        {
        }

        public static ImageInfo Create(Image model, string repoName, ManifestFilter manifestFilter, VariableHelper variableHelper)
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

            imageInfo.Platforms = manifestFilter.GetPlatforms(model)
                .Select(platform => PlatformInfo.Create(platform, repoName, variableHelper))
                .ToArray();

            IEnumerable<Platform> activePlatformModels = manifestFilter.GetActivePlatforms(model);
            imageInfo.ActivePlatforms = imageInfo.Platforms
                    .Where(platform => activePlatformModels.Contains(platform.Model));

            return imageInfo;
        }
    }
}
