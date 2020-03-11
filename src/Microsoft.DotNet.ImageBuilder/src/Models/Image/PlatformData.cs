// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Image
{
    public class PlatformData
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SortedDictionary<string, string> BaseImages { get; set; }

        public List<string> SimpleTags { get; set; } = new List<string>();

        public string Digest { get; set; }

        [JsonIgnore]
        public IEnumerable<string> FullyQualifiedSimpleTags { get; set; }

        [JsonIgnore]
        public IEnumerable<string> AllTags { get; set; }
    }
}
