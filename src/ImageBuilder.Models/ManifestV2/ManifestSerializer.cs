// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// Reads and writes YAML manifests.
/// </summary>
public static partial class ManifestSerializer
{
    // Without type inference, every scalar stays a string, so "10.0" isn't read as a number.
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithQuotingNecessaryStrings()
        .WithEventEmitter(next => new FlowStringListEmitter(next))
        .Build();

    // YAML goes through JSON so that both directions share one set of names and rules.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Reads and validates the manifest at <paramref name="filePath"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">The manifest is invalid.</exception>
    public static Manifest Load(string filePath)
    {
        string yaml = File.ReadAllText(filePath);
        object document = YamlDeserializer.Deserialize<object>(yaml)
            ?? throw new InvalidDataException("The manifest must not be empty.");

        Manifest manifest;
        try
        {
            manifest = JsonSerializer.SerializeToElement(document).Deserialize<Manifest>(JsonOptions)
                ?? throw new InvalidDataException("The manifest must not be null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{filePath}: {exception.Message}", exception);
        }

        Validate(manifest);
        return manifest;
    }

    /// <summary>
    /// Writes <paramref name="manifest"/> as YAML, leaving out empty and unset values.
    /// </summary>
    public static string Serialize(Manifest manifest)
    {
        JsonNode manifestJson = JsonSerializer.SerializeToNode(manifest, JsonOptions)
            ?? throw new ArgumentException("The manifest must not be null.", nameof(manifest));
        object document = ToYamlValue(manifestJson)
            ?? throw new ArgumentException("The manifest must not be empty.", nameof(manifest));
        return YamlSerializer.Serialize(document);
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
                foreach ((string platformKey, Platform platform) in image.Platforms)
                {
                    string platformId = $"{imageId}{ImageEntry.KeySeparator}{platformKey}";
                    if (!OciPlatformRegex().IsMatch(platformKey))
                    {
                        throw new InvalidDataException(
                            $"{platformId}: platforms must be keyed by OCI platform, like 'linux/amd64' or 'linux/arm/v7'.");
                    }

                    ClaimTags([.. platform.Tags, .. platform.UndocumentedTags], platformId);
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

    // Converts JSON to the plain dictionaries, lists and strings YamlDotNet writes, dropping empty values.
    private static object? ToYamlValue(JsonNode? node) => node switch
    {
        JsonObject jsonObject => EmptyToNull(jsonObject
            .Select(property => (property.Key, Value: ToYamlValue(property.Value)))
            .Where(property => property.Value is not null)
            .ToDictionary(property => property.Key, property => property.Value)),
        JsonArray jsonArray => EmptyToNull(jsonArray.Select(ToYamlValue).OfType<object>().ToList()),
        JsonValue jsonValue => jsonValue.ToString(),
        _ => null,
    };

    private static object? EmptyToNull<T>(ICollection<T> collection) => collection.Count == 0 ? null : collection;

    [GeneratedRegex(@"^[a-z0-9]+/[a-z0-9]+(/[a-z0-9]+)?$")]
    private static partial Regex OciPlatformRegex();

    // Writes lists of strings, like tags, on one line.
    private sealed class FlowStringListEmitter(IEventEmitter next) : ChainedEventEmitter(next)
    {
        public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
        {
            if (eventInfo.Source.Value is List<object> items && items.All(item => item is string))
            {
                eventInfo.Style = SequenceStyle.Flow;
            }

            base.Emit(eventInfo, emitter);
        }
    }
}
