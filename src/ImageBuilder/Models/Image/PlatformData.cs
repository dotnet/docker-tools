// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.DotNet.DockerTools.ImageBuilder.ViewModel;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Models.Image
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

        [JsonProperty(Required = Required.Always)]
        public string Dockerfile { get; set; } = string.Empty;

        public List<string> SimpleTags { get; set; } = new();

        [JsonProperty(Required = Required.Always)]
        public string Digest { get; set; } = string.Empty;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? BaseImageDigest { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string OsType { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always)]
        public string OsVersion { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always)]
        public string Architecture { get; set; } = string.Empty;

        public DateTime Created { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string CommitUrl { get; set; } = string.Empty;

        public List<string> Layers { get; set; } = new();

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
        public ImageInfo? ImageInfo { get; set; }

        [JsonIgnore]
        public PlatformInfo? PlatformInfo { get; set; }

        [JsonIgnore]
        public IEnumerable<TagInfo> AllTags =>
            ImageInfo?.SharedTags.Union(PlatformInfo?.Tags ?? Enumerable.Empty<TagInfo>()) ?? Enumerable.Empty<TagInfo>();

        public int CompareTo([AllowNull] PlatformData other)
        {
            if (other is null)
            {
                return 1;
            }


            if (HasDifferentTagState(other))
            {
                return 1;
            }

            return GetIdentifier().CompareTo(other.GetIdentifier());
        }

        // Product versions are considered equivalent if the major and minor segments are the same
        // See https://github.com/dotnet/docker-tools/issues/688
        public string GetIdentifier(bool excludeProductVersion = false) =>
            $"{Dockerfile}-{Architecture}-{OsType}-{OsVersion}{(excludeProductVersion ? "" : "-" + GetMajorMinorVersion())}";

        public bool HasDifferentTagState(PlatformData other) =>
            // If either of the platforms has no simple tags while the other does have simple tags, they are not equal
            IsNullOrEmpty(SimpleTags) && !IsNullOrEmpty(other.SimpleTags) ||
            !IsNullOrEmpty(SimpleTags) && IsNullOrEmpty(other.SimpleTags);

        public static PlatformData FromPlatformInfo(PlatformInfo platform, ImageInfo image) =>
            new PlatformData(image, platform)
            {
                Dockerfile = platform.DockerfilePathRelativeToManifest,
                Architecture = platform.Model.Architecture.GetDisplayName(),
                OsType = platform.Model.OS.ToString(),
                OsVersion = platform.Model.OsVersion,
                SimpleTags = platform.Tags.Select(tag => tag.Name).ToList()
            };

        private bool IsNullOrEmpty<T>(List<T>? list) =>
            list is null || !list.Any();

        private string? GetMajorMinorVersion()
        {
            if (ImageInfo is null)
            {
                return null;
            }

            string? fullVersion = ImageInfo.ProductVersion;

            if (string.IsNullOrEmpty(fullVersion))
            {
                return null;
            }

            // Remove any version suffix (like "-preview")
            int separatorIndex = fullVersion.IndexOf("-");
            if (separatorIndex >= 0)
            {
                fullVersion = fullVersion.Substring(0, separatorIndex);
            }

            return new Version(fullVersion).ToString(2);
        }
    }
}
#nullable disable
