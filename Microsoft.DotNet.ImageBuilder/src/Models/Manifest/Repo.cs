// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest
{
    public class Repo
    {
        public string Id { get; set; }

        [JsonProperty(Required = Required.Always)]
        public Image[] Images { get; set; }

        public string McrTagsMetadataTemplatePath { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }

        public string ReadmePath { get; set; }

        public Repo()
        {
        }
    }
}
