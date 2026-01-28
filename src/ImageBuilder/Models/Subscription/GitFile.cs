#nullable disable
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Subscription
{
    public class GitFile : IGitHubFileRef
    {
        [JsonProperty(Required = Required.Always)]
        public string Owner { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Repo { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Branch { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Path { get; set; }
    }
}
