// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class ImageInfo
    {
        public PlatformInfo Platform { get; private set; }
        public IEnumerable<string> Tags { get; private set; }
        public Image Model { get; private set; }

        private ImageInfo()
        {
        }

        public static ImageInfo Create(Image model, string dockerOS, Repo repo)
        {
            ImageInfo imageInfo = new ImageInfo();
            imageInfo.Model = model;
            imageInfo.Tags = model.SharedTags.Select(tag => $"{repo.DockerRepo}:{tag}");

            if (model.Platforms.TryGetValue(dockerOS, out Platform platform))
            {
                imageInfo.Platform = PlatformInfo.Create(platform, repo);
                imageInfo.Tags = imageInfo.Tags.Concat(imageInfo.Platform.Tags);
            }

            imageInfo.Tags = imageInfo.Tags.ToArray();

            return imageInfo;
        }

        public override string ToString()
        {
            return
$@"Tags:
  {string.Join($"{Environment.NewLine}  ", Tags)}
Platform (
{Platform?.ToString()}
)";
        }
    }
}
