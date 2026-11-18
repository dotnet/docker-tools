// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// Reads and writes YAML and JSON manifests.
/// </summary>
public static partial class ManifestSerializer
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithEnforceRequiredMembers()
        .WithEnforceNullability()
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .DisableAliases()
        .WithQuotingNecessaryStrings()
        .WithEventEmitter(next => new FlowStyleEmitter(next))
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitEmptyCollections } },
    };

    /// <summary>
    /// Reads and validates the manifest at <paramref name="filePath"/>, as JSON if it ends in ".json"
    /// and as YAML if it ends in ".yaml" or ".yml".
    /// </summary>
    /// <exception cref="InvalidDataException">The manifest is invalid.</exception>
    public static Manifest Load(string filePath)
    {
        string content = File.ReadAllText(filePath);
        Manifest manifest = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".json" => DeserializeJson(content, filePath),
            ".yaml" or ".yml" => DeserializeYaml(content, filePath),
            string extension => throw new ArgumentException(
                $"{filePath}: '{extension}' is not a manifest extension. Use .json, .yaml or .yml.", nameof(filePath)),
        };

        Validate(manifest);
        return manifest;
    }

    /// <summary>
    /// Writes <paramref name="manifest"/> as YAML, leaving out empty and unset values.
    /// </summary>
    public static string SerializeYaml(Manifest manifest) => YamlSerializer.Serialize(manifest);

    /// <summary>
    /// Writes <paramref name="manifest"/> as JSON, leaving out empty and unset values.
    /// </summary>
    public static string SerializeJson(Manifest manifest) => JsonSerializer.Serialize(manifest, JsonOptions) + "\n";

    private static Manifest DeserializeYaml(string yaml, string filePath)
    {
        try
        {
            return YamlDeserializer.Deserialize<Manifest?>(yaml)
                ?? throw new InvalidDataException($"{filePath}: the manifest must not be empty.");
        }
        catch (YamlException exception)
        {
            string detail = exception.InnerException is null ? "" : $" {exception.InnerException.Message}";
            throw new InvalidDataException($"{filePath}: {exception.Message}{detail}", exception);
        }
    }

    private static Manifest DeserializeJson(string json, string filePath)
    {
        try
        {
            return JsonSerializer.Deserialize<Manifest>(json, JsonOptions)
                ?? throw new InvalidDataException($"{filePath}: the manifest must not be null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{filePath}: {exception.Message}", exception);
        }
    }

    private static void OmitEmptyCollections(JsonTypeInfo typeInfo)
    {
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (property.PropertyType.IsAssignableTo(typeof(ICollection)))
            {
                property.ShouldSerialize = (_, value) => value is ICollection { Count: > 0 };
            }
        }
    }

    private static void Validate(Manifest manifest)
    {
        foreach ((string repoKey, Repo repo) in manifest.Repos)
        {
            Dictionary<string, string> tagOwners = new(StringComparer.Ordinal);
            foreach ((string imageKey, Image image) in repo.Images)
            {
                string imageId = $"{repoKey}{ImageEntry.KeySeparator}{imageKey}";
                if (image.Platforms.Count == 0)
                {
                    throw new InvalidDataException($"{imageId}: an image must have at least one platform.");
                }

                ClaimTags(image.SharedTags, imageId);
                HashSet<string> platforms = [];
                foreach ((string platformKey, ImagePlatform imagePlatform) in image.Platforms)
                {
                    string platformId = $"{imageId}{ImageEntry.KeySeparator}{platformKey}";
                    if (!PlatformRegex.IsMatch(imagePlatform.Platform))
                    {
                        throw new InvalidDataException(
                            $"{platformId}: platform '{imagePlatform.Platform}' must look like 'linux/amd64' or 'linux/arm/v7'.");
                    }

                    if (!platforms.Add(imagePlatform.Platform))
                    {
                        throw new InvalidDataException(
                            $"{imageId}: more than one platform is {imagePlatform.Platform}.");
                    }

                    ClaimTags([.. imagePlatform.Tags, .. imagePlatform.UndocumentedTags], platformId);
                }
            }

            void ClaimTags(string[] tags, string ownerId)
            {
                foreach (string tag in tags)
                {
                    if (!tagOwners.TryAdd(tag, ownerId))
                    {
                        throw new InvalidDataException(
                            $"{repoKey}: tag '{tag}' is declared by both {tagOwners[tag]} and {ownerId}.");
                    }
                }
            }
        }
    }

    [GeneratedRegex(@"^[a-z0-9]+/[a-z0-9]+(/[a-z0-9]+)?$")]
    private static partial Regex PlatformRegex { get; }

    private sealed class FlowStyleEmitter(IEventEmitter next) : ChainedEventEmitter(next)
    {
        public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
        {
            if (eventInfo.Source.Type == typeof(string[]))
            {
                eventInfo.Style = SequenceStyle.Flow;
            }

            base.Emit(eventInfo, emitter);
        }
    }
}
