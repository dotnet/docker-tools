// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.DotNet.ImageBuilder.ReadModel.Serialization;

internal static class JsonHelper
{
    public static T Deserialize<T>(JsonNode jsonNode, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Deserialize(jsonNode, typeInfo)
            ?? throw new Exception($"Failed to deserialize JSON object to {typeof(T)}.");

    public static string Serialize<T>(T model, JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Serialize(model, typeInfo);
}
