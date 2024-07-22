// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Models.Annotations
{
    public record EolDigestData
    {
        [JsonProperty(Required = Required.Always)]
        public string Digest { get; init; } = string.Empty;

        // This isn't read from programmatically, but is useful for debugging
        public string? Tag { get; init; }

        public DateOnly? EolDate { get; init; }

        public EolDigestData()
        {
        }

        public EolDigestData(string digest)
        {
            Digest = digest;
        }
    }
}
