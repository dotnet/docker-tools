// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Model
{
    public class Manifest
    {
        [JsonProperty(Required = Required.Always)]
        public Repo[] Repos { get; set; }

        public IDictionary<string, string> Variables { get; set; }

        [JsonConverter(typeof(EnumKeyDictionaryConverter<OS>))]
        public IDictionary<OS, string[]> TestCommands { get; set; }

        public Manifest()
        {
        }
    }
}
