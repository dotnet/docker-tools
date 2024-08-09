// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Models.Oci;

public record Manifest
{
    public string ArtifactType { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public Dictionary<string, string> Annotations { get; init; } = [];

    public static Manifest FromJson(string json)
    {
        return JsonConvert.DeserializeObject<Manifest>(json) ??
            throw new InvalidOperationException("Unable to deserialize manifest");
    }   
}
