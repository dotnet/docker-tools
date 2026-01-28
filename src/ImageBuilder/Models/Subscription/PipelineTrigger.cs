#nullable disable
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Subscription
{
    public class PipelineTrigger
    {
        [JsonProperty(Required = Required.Always)]
        public int Id { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string PathVariable { get; set; }
    }
}
