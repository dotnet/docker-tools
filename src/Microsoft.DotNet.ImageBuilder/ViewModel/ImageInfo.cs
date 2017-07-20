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
        public IEnumerable<string> ActiveTags { get; private set; }
        public Image Model { get; private set; }
        public IEnumerable<string> SharedTags { get; private set; }

        private ImageInfo()
        {
        }

        public static ImageInfo Create(
            Image model,
            Manifest manifest,
            string repoName,
            Architecture dockerArchitecture,
            string dockerOS,
            string includePath)
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

            imageInfo.ActivePlatform = model.Platforms
                .Where(platform => platform.OS == dockerOS && platform.Architecture == dockerArchitecture)
                .Where(platform => string.IsNullOrWhiteSpace(includePath) || platform.Dockerfile.StartsWith(includePath))
                .Select(platform => PlatformInfo.Create(platform, manifest, repoName))
                .SingleOrDefault();

            if (imageInfo.ActivePlatform != null)
            {
                imageInfo.ActiveTags = imageInfo.SharedTags.Concat(imageInfo.ActivePlatform.Tags);
            }

            return imageInfo;
        }
    }
}
