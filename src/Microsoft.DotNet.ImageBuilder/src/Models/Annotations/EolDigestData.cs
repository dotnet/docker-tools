// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.EolAnnotations
{
    public class EolDigestData
    {
        [JsonProperty(Required = Required.Always)]
        public string Digest { get; set; }

        public DateOnly? EolDate { get; set; }
    }
}
