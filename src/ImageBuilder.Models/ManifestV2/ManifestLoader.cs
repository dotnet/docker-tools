// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// Reads a YAML manifest and resolves its defaults, metadata and patterns.
/// </summary>
public static class ManifestLoader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    // Also names and formats the model values that placeholders can refer to.
    internal static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>
    /// Reads and resolves the manifest at <paramref name="filePath"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">The manifest is invalid.</exception>
    public static Manifest Load(string filePath)
    {
        string yaml = File.ReadAllText(filePath);
        Manifest manifest = Parse(yaml);
        ManifestResolver resolver = new(manifest);
        return resolver.Resolve();
    }

    private static Manifest Parse(string yaml)
    {
        using StringReader reader = new(yaml);
        Parser yamlParser = new(reader);
        MergingParser mergingParser = new(yamlParser);

        Dictionary<string, object?> manifestDocument =
            YamlDeserializer.Deserialize<Dictionary<string, object?>>(mergingParser)
                ?? throw new InvalidDataException("The manifest must not be null.");

        // Expand YAML reuse before removing authoring-only definitions and validating the model.
        manifestDocument.Remove("definitions");

        JsonElement manifestJson = JsonSerializer.SerializeToElement(manifestDocument);
        return manifestJson.Deserialize<Manifest>(JsonOptions)
            ?? throw new InvalidDataException("The manifest must not be null.");
    }

    private static void ConfigureProperties(JsonTypeInfo typeInfo)
    {
        bool isPlatformType = typeInfo.Type == typeof(Platform) || typeInfo.Type == typeof(PlatformDefaults);
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            bool isTags = property.PropertyType == typeof(Dictionary<string, Tag>);
            bool isDockerfilePath = isPlatformType && property.Name is "dockerfile" or "dockerfileTemplate";

            // Omission enables inheritance; explicit null is not a valid override.
            if (isTags || isDockerfilePath)
            {
                property.IsSetNullable = false;
            }
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        DefaultJsonTypeInfoResolver typeInfoResolver = new();
        typeInfoResolver.Modifiers.Add(ConfigureProperties);
        JsonStringEnumConverter enumConverter = new(JsonNamingPolicy.CamelCase, allowIntegerValues: false);

        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            TypeInfoResolver = typeInfoResolver
        };

        options.Converters.Add(enumConverter);
        return options;
    }
}
