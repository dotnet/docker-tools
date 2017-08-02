// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Model
{
    public class Tag
    {
        public bool IsUndocumented { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }

        public Tag()
        {
        }
    }
}
