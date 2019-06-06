﻿using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest
{
    public class CustomBuildLegGrouping
    {
        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string[] Dependencies { get; set; }
    }
}
