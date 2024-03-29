﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace FilePusher.Models
{
    public class GitRepo
    {
        [JsonProperty(Required = Required.Always)]
        public string Owner { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always)]
        public string Branch { get; set; } = string.Empty;

        public override string ToString() => $"{Owner}/{Name}/{Branch}";
    }
}
