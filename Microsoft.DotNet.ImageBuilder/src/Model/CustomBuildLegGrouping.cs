using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ImageBuilder.Model
{
    public class CustomBuildLegGrouping
    {
        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string[] Dependencies { get; set; }
    }
}
