// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ImageBuilder.Models.Image;

public class ImageArtifactDetails
{
    private static readonly JsonSerializerSettings s_jsonSettings = new()
    {
        Converters =
        [
            new SchemaVersion2LayerConverter()
        ]
    };

    public string SchemaVersion => "2.0";

    public List<RepoData> Repos { get; set; } = [];

    public static ImageArtifactDetails FromJson(string json)
    {
        ImageArtifactDetails imageArtifactDetails =
            JsonConvert.DeserializeObject<ImageArtifactDetails>(json, s_jsonSettings)
                ?? throw new SerializationException(
                    $"""
                    Failed to deserialize {nameof(ImageArtifactDetails)} from content:
                    {json}
                    """);

        return imageArtifactDetails;
    }

    private class SchemaVersion2LayerConverter : JsonConverter
    {
        // We do not want to handle writing at all. We only want to convert
        // the old Layer format to the new format, and all writing should be
        // done using the new format (via the default conversion settings).
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Layer);
        }

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            return token.Type switch
            {
                // If the token is an object, proceed as normal
                JTokenType.Object => JsonHelper.SerializeObject(token),

                // If we encounter a string, we want to convert it to the Layer
                // object defined in schema version 2. Assume a size of 0. The
                // next time an image is built, the size will be updated.
                JTokenType.String =>
                    new Layer(
                        Digest: token.Value<string>()
                            ?? throw new JsonSerializationException(
                                $"Unable to serialize digest from '{token}'"),
                        Size: 0),

                // Handle null and other token types
                JTokenType.Null => null,
                _ => throw new JsonSerializationException(
                        $"Unexpected token type: {token.Type} when parsing Layer.")
            };
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
