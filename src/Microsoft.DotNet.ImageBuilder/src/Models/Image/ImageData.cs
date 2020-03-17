﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Image
{
    public class ImageData : IComparable<ImageData>
    {
        public List<PlatformData> Platforms { get; set; } = new List<PlatformData>();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProductVersion { get; set; }

        /// <summary>
        /// Gets or sets a reference to the corresponding image definition in the manifest.
        /// </summary>
        [JsonIgnore]
        public ImageInfo ManifestImage { get; set; }

        public int CompareTo([AllowNull] ImageData other)
        {
            if (other is null)
            {
                return 1;
            }

            if (ManifestImage == other.ManifestImage)
            {
                return 0;
            }

            // If we're comparing two different image items, compare them by the first Platform to
            // provide deterministic ordering.
            PlatformData thisFirstPlatform = Platforms
                .OrderBy(platform => platform)
                .FirstOrDefault();
            PlatformData otherFirstPlatform = other.Platforms
                .OrderBy(platform => platform)
                .FirstOrDefault();
            return thisFirstPlatform?.CompareTo(otherFirstPlatform) ?? 1;
        }
    }
}
