// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

/// <summary>
/// Reads and writes JSON manifests.
/// </summary>
public static partial class ManifestSerializer
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitEmptyCollections } },
    };

    /// <summary>
    /// Reads and validates the manifest at <paramref name="filePath"/>.
    /// </summary>
    /// <exception cref="JsonException">The manifest is not valid JSON or does not match the model.</exception>
    /// <exception cref="InvalidDataException">The manifest is invalid.</exception>
    public static Manifest Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        Manifest manifest = JsonSerializer.Deserialize<Manifest>(json, JsonOptions)
            ?? throw new InvalidDataException($"{filePath}: the manifest must not be null.");

        Validate(manifest);
        return manifest;
    }

    /// <summary>
    /// Writes <paramref name="manifest"/> as JSON, leaving out empty and unset values.
    /// </summary>
    public static string Serialize(Manifest manifest) => JsonSerializer.Serialize(manifest, JsonOptions);

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
        foreach (IGrouping<string, ImageKey> repoImageKeys in manifest.GetImageKeys().GroupBy(imageKey => imageKey.Repo))
        {
            Dictionary<string, string> tagOwners = new(StringComparer.Ordinal);
            foreach (ImageKey imageKey in repoImageKeys)
            {
                Image image = manifest.GetImage(imageKey);
                if (!KeyPatterns.ImageKey.IsMatch(imageKey.ToString()))
                {
                    throw new InvalidDataException(
                        $"{imageKey}: repo and image keys must be {KeyPatterns.Description}.");
                }

                if (image.Platforms.Count == 0)
                {
                    throw new InvalidDataException($"{imageKey}: an image must have at least one platform.");
                }

                ClaimTags(image.SharedTags, imageKey.ToString());
                HashSet<string> platforms = [];
                foreach ((string platformName, ImagePlatform imagePlatform) in image.Platforms)
                {
                    PlatformKey platformKey = new(imageKey.Repo, imageKey.Image, platformName);
                    if (!KeyPatterns.PlatformKey.IsMatch(platformKey.ToString()))
                    {
                        throw new InvalidDataException(
                            $"{platformKey}: platform keys must be {KeyPatterns.Description}.");
                    }

                    if (!PlatformRegex.IsMatch(imagePlatform.Platform))
                    {
                        throw new InvalidDataException(
                            $"{platformKey}: platform '{imagePlatform.Platform}' must look like 'linux/amd64' or 'linux/arm/v7'.");
                    }

                    if (!platforms.Add(imagePlatform.Platform))
                    {
                        throw new InvalidDataException($"{imageKey}: more than one platform is {imagePlatform.Platform}.");
                    }

                    ClaimTags([.. imagePlatform.Tags, .. imagePlatform.UndocumentedTags], platformKey.ToString());
                }
            }

            void ClaimTags(string[] tags, string owner)
            {
                foreach (string tag in tags)
                {
                    if (!tagOwners.TryAdd(tag, owner))
                    {
                        throw new InvalidDataException(
                            $"{repoImageKeys.Key}: tag '{tag}' is declared by both {tagOwners[tag]} and {owner}.");
                    }
                }
            }
        }
    }

    [GeneratedRegex(@"^[a-z0-9]+/[a-z0-9]+(/[a-z0-9]+)?$")]
    private static partial Regex PlatformRegex { get; }
}
