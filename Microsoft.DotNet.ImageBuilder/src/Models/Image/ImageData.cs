// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Image
{
    public class ImageData
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SortedDictionary<string, string> BaseImages { get; set; }
        public List<string> SimpleTags { get; set; } = new List<string>();
        public string Digest;
    }
}
