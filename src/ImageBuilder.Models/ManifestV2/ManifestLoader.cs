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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { ConfigureProperties }
        },
        Converters =
        {
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false)
        }
    };

    /// <summary>
    /// Reads and resolves the manifest at <paramref name="filePath"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">The manifest is invalid.</exception>
    public static async Task<Manifest> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        string yaml = await File.ReadAllTextAsync(filePath, cancellationToken);
        return new ManifestResolver(Parse(yaml)).Resolve();
    }

    private static Manifest Parse(string yaml)
    {
        using StringReader reader = new(yaml);
        MergingParser parser = new(new Parser(reader));
        Dictionary<string, object?> document = YamlDeserializer.Deserialize<Dictionary<string, object?>>(parser)
            ?? throw new InvalidDataException("The manifest must not be null.");

        // Expand YAML reuse before removing authoring-only definitions and validating the model.
        document.Remove("definitions");
        return JsonSerializer.SerializeToElement(document).Deserialize<Manifest>(JsonOptions)
            ?? throw new InvalidDataException("The manifest must not be null.");
    }

    private static void ConfigureProperties(JsonTypeInfo typeInfo)
    {
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            // Omission enables inheritance; explicit null is not a valid override.
            if (property.PropertyType == typeof(Dictionary<string, Tag>)
                || ((typeInfo.Type == typeof(Platform) || typeInfo.Type == typeof(PlatformDefaults))
                    && property.Name is "dockerfile" or "dockerfileTemplate"))
            {
                property.IsSetNullable = false;
            }
        }
    }
}
