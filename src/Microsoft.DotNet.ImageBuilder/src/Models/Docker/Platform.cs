// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Docker
{
    public partial class Platform
    {
        public string Architecture { get; set; }

        public string Os { get; set; }

        [JsonProperty("os.version")]
        public string OsVersion { get; set; }
    }
}
