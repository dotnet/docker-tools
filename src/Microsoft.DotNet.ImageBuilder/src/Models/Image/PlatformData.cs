// Licensed to the .NET Foundation under one or more agreements.
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
    public class PlatformData : IComparable<PlatformData>
    {
        public PlatformData()
        {
        }

        public PlatformData(ImageInfo imageInfo, PlatformInfo platformInfo)
        {
            ImageInfo = imageInfo;
            PlatformInfo = platformInfo;
        }

        public string Dockerfile { get; set; }

        public List<string> SimpleTags { get; set; } = new List<string>();

        public string Digest { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string BaseImageDigest { get; set; }

        public string OsType { get; set; }

        public string OsVersion { get; set; }

        public string Architecture { get; set; }

        public DateTime Created { get; set; }

        public string CommitUrl { get; set; }

        /// <summary>
        /// Gets or sets whether the image or its associated tag names have changed since it was last published.
        /// </summary>
        /// <remarks>
        /// Items with this state should only be used internally within a build. Such items
        /// should be stripped out of the published image info content.
        /// </remarks>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsUnchanged { get; set; }

        [JsonIgnore]
        public ImageInfo ImageInfo { get; set; }

        [JsonIgnore]
        public PlatformInfo PlatformInfo { get; set; }

        [JsonIgnore]
        public IEnumerable<TagInfo> AllTags =>
            ImageInfo?.SharedTags.Union(PlatformInfo.Tags ?? Enumerable.Empty<TagInfo>()) ?? Enumerable.Empty<TagInfo>();

        public int CompareTo([AllowNull] PlatformData other)
        {
            if (other is null)
            {
                return 1;
            }

            // If either of the platforms has no simple tags while the other does have simple tags, they are not equal
            if ((SimpleTags?.Count == 0 && other.SimpleTags?.Count > 0) ||
                (SimpleTags?.Count > 0 && other.SimpleTags?.Count == 0))
            {
                return 1;
            }

            return GetIdentifier().CompareTo(other.GetIdentifier());
        }

        public bool Equals(PlatformInfo platformInfo) =>
            CompareTo(FromPlatformInfo(platformInfo, null)) == 0;

        public string GetIdentifier() => $"{Dockerfile}-{Architecture}-{OsType}-{OsVersion}";

        public static PlatformData FromPlatformInfo(PlatformInfo platform, ImageInfo image) =>
            new PlatformData(image, platform)
            {
                Dockerfile = platform.DockerfilePathRelativeToManifest,
                Architecture = platform.Model.Architecture.GetDisplayName(),
                OsType = platform.Model.OS.ToString(),
                OsVersion = platform.Model.OsVersion,
                SimpleTags = platform.Tags.Select(tag => tag.Name).ToList()
            };
    }
}
