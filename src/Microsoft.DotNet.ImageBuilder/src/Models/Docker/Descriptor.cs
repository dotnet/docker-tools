// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Docker
{
    public partial class Descriptor : IEquatable<Descriptor>
    {
        public string MediaType { get; set; }

        public string Digest { get; set; }

        public long Size { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Platform Platform { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Uri[] Urls { get; set; }

        public bool Equals(Descriptor other)
        {
            return other != null && Digest == other.Digest;
        }

        public override bool Equals(object obj) => Equals(obj as Descriptor);

        public override int GetHashCode() => Digest.GetHashCode();
    }
}
