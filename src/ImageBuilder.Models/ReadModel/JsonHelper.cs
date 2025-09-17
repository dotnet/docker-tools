// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

internal static class JsonHelper
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static T Deserialize<T>(JsonNode jsonNode) => JsonSerializer.Deserialize<T>(jsonNode, s_jsonOptions)
        ?? throw new Exception($"Failed to deserialize JSON object to {typeof(T)}.");

    public static string Serialize<T>(T model) => JsonSerializer.Serialize(model, s_jsonOptions);
}
