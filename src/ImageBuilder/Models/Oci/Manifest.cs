// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Oci;

public record Manifest
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public string ArtifactType { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public Dictionary<string, string> Annotations { get; init; } = [];

    public static Manifest FromJson(string json)
    {
        return JsonSerializer.Deserialize<Manifest>(json, s_jsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize manifest");
    }   
}
