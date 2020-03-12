// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Image
{
    public class PlatformData : IComparable<PlatformData>
    {
        public string Path { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SortedDictionary<string, string> BaseImages { get; set; }

        public List<string> SimpleTags { get; set; } = new List<string>();

        public string Digest { get; set; }

        public string Architecture { get; set; }
        
        public string OsType { get; set; }
        
        public string OsVersion { get; set; }

        [JsonIgnore]
        public IEnumerable<string> FullyQualifiedSimpleTags { get; set; }

        [JsonIgnore]
        public IEnumerable<string> AllTags { get; set; }

        public int CompareTo([AllowNull] PlatformData other)
        {
            if (other is null)
            {
                return 1;
            }

            return GetIdentifier().CompareTo(other.GetIdentifier());
        }

        private string GetIdentifier() => $"{Path}-{Architecture}-{OsType}-{OsVersion}";
    }
}
