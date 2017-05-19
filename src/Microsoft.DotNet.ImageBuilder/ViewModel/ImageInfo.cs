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
        public Image Model { get; private set; }
        public PlatformInfo Platform { get; private set; }
        public IEnumerable<string> Tags { get; private set; }

        private ImageInfo()
        {
        }

        public static ImageInfo Create(Image model, string repoName, string dockerOS)
        {
            ImageInfo imageInfo = new ImageInfo();
            imageInfo.Model = model;

            if (model.SharedTags == null)
            {
                imageInfo.Tags = Enumerable.Empty<string>();
            }
            else
            {
                imageInfo.Tags = model.SharedTags.Select(tag => $"{repoName}:{tag}");
            }

            if (model.Platforms.TryGetValue(dockerOS, out Platform platform))
            {
                imageInfo.Platform = PlatformInfo.Create(platform, repoName);
                imageInfo.Tags = imageInfo.Tags.Concat(imageInfo.Platform.Tags);
            }

            imageInfo.Tags = imageInfo.Tags.ToArray();

            return imageInfo;
        }
    }
}
