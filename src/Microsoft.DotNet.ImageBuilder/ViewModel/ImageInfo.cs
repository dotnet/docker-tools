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
        public PlatformInfo ActivePlatform { get; private set; }
        public IEnumerable<string> ActiveFullyQualifiedTags { get; private set; }
        public Image Model { get; private set; }
        public IEnumerable<PlatformInfo> Platforms { get; set; }
        public IEnumerable<TagInfo> SharedTags { get; private set; }

        private ImageInfo()
        {
        }

        public static ImageInfo Create(Image model, Manifest manifest, string repoName, ManifestFilter manifestFilter)
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
                    .Select(tag => TagInfo.Create(tag, manifest, repoName))
                    .ToArray();
            }

            imageInfo.Platforms = manifestFilter.GetPlatforms(model)
                .Select(platform => PlatformInfo.Create(platform, manifest, repoName))
                .ToArray();

            Platform activePlatformModel = manifestFilter.GetActivePlatform(model);
            if (activePlatformModel != null)
            {
                imageInfo.ActivePlatform = imageInfo.Platforms
                    .First(platform => platform.Model == activePlatformModel);
                imageInfo.ActiveFullyQualifiedTags = imageInfo.SharedTags
                    .Select(tag => tag.FullyQualifiedName)
                    .Concat(imageInfo.ActivePlatform.Tags.Select(tag => tag.FullyQualifiedName));
            }

            return imageInfo;
        }
    }
}
