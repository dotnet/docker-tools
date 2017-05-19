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
        public IEnumerable<string> AllTags { get; private set; }
        public Image Model { get; private set; }
        public PlatformInfo Platform { get; private set; }
        public IEnumerable<string> SharedTags { get; private set; }

        private ImageInfo()
        {
        }

        public static ImageInfo Create(Image model, Manifest manifest, string repoName, string dockerOS)
        {
            ImageInfo imageInfo = new ImageInfo();
            imageInfo.Model = model;

            if (model.SharedTags == null)
            {
                imageInfo.SharedTags = Enumerable.Empty<string>();
            }
            else
            {
                imageInfo.SharedTags = model.SharedTags
                    .Select(tag => $"{repoName}:{manifest.SubstituteTagVariables(tag)}")
                    .ToArray();
            }

            imageInfo.AllTags = imageInfo.SharedTags;

            if (model.Platforms.TryGetValue(dockerOS, out Platform platform))
            {
                imageInfo.Platform = PlatformInfo.Create(platform, manifest, repoName);
                imageInfo.AllTags = imageInfo.AllTags
                    .Concat(imageInfo.Platform.Tags)
                    .ToArray();
            }

            return imageInfo;
        }
    }
}
