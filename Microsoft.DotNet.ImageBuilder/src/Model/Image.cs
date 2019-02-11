// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Model
{
    public class Image
    {
        [JsonProperty(Required = Required.Always)]
        public Platform[] Platforms { get; set; }

        public IDictionary<string, Tag> SharedTags { get; set; }

        public Image()
        {
        }
    }
}
