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
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitEmptyCollections } },
    };

    /// <summary>
    /// Reads and validates the manifest at <paramref name="filePath"/>.
    /// </summary>
    /// <exception cref="JsonException">The manifest is not valid JSON or does not match the model.</exception>
    /// <exception cref="InvalidDataException">The manifest is invalid. The message lists every problem.</exception>
    public static Manifest Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        Manifest manifest = JsonSerializer.Deserialize<Manifest>(json, JsonOptions)
            ?? throw new InvalidDataException($"{filePath}: the manifest must not be null.");

        List<string> errors = Validate(manifest);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"{filePath} has {errors.Count} error(s):{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

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

    private static List<string> Validate(Manifest manifest)
    {
        List<string> errors = [];

        foreach (IGrouping<string, ImageKey> repoImageKeys in manifest.GetImageKeys().GroupBy(imageKey => imageKey.Repo))
        {
            List<(string Tag, string DeclaredBy)> tagDeclarations = [];

            foreach (ImageKey imageKey in repoImageKeys)
            {
                Image image = manifest.GetImage(imageKey);

                if (!KeyPatterns.ImageKey.IsMatch(imageKey.ToString()))
                {
                    errors.Add($"{nameof(Image)} '{imageKey}': repo and image keys must be {KeyPatterns.Description}.");
                }

                if (image.Platforms.Count == 0)
                {
                    errors.Add($"{nameof(Image)} '{imageKey}': an image must have at least one platform.");
                }

                tagDeclarations.AddRange(image.SharedTags.Select(tag => (tag, $"{nameof(Image)} '{imageKey}'")));

                foreach ((string platformName, ImagePlatform imagePlatform) in image.Platforms)
                {
                    PlatformKey platformKey = new(imageKey.Repo, imageKey.Image, platformName);

                    if (!KeyPatterns.PlatformKey.IsMatch(platformKey.ToString()))
                    {
                        errors.Add($"{nameof(ImagePlatform)} '{platformKey}': platform keys must be {KeyPatterns.Description}.");
                    }

                    if (!PlatformRegex.IsMatch(imagePlatform.Platform))
                    {
                        errors.Add(
                            $"{nameof(ImagePlatform)} '{platformKey}': platform '{imagePlatform.Platform}' must look like 'linux/amd64' or 'linux/arm/v7'.");
                    }

                    tagDeclarations.AddRange(
                        imagePlatform.Tags
                            .Concat(imagePlatform.UndocumentedTags)
                            .Select(tag => (tag, $"{nameof(ImagePlatform)} '{platformKey}'")));
                }

                IEnumerable<IGrouping<string, string>> duplicatePlatforms = image.Platforms
                    .GroupBy(platform => platform.Value.Platform, platform => platform.Key)
                    .Where(group => group.Count() > 1);

                foreach (IGrouping<string, string> duplicatePlatform in duplicatePlatforms)
                {
                    errors.Add(
                        $"{nameof(Image)} '{imageKey}': platforms {string.Join(", ", duplicatePlatform.Select(name => $"'{name}'"))}"
                        + $" are all '{duplicatePlatform.Key}'.");
                }
            }

            IEnumerable<IGrouping<string, string>> duplicateTags = tagDeclarations
                .GroupBy(declaration => declaration.Tag, declaration => declaration.DeclaredBy, StringComparer.Ordinal)
                .Where(group => group.Count() > 1);

            foreach (IGrouping<string, string> duplicateTag in duplicateTags)
            {
                errors.Add($"{nameof(Repo)} '{repoImageKeys.Key}': tag '{duplicateTag.Key}' is declared by {string.Join(", ", duplicateTag)}.");
            }
        }

        return errors;
    }

    [GeneratedRegex(@"^[a-z0-9]+/[a-z0-9]+(/[a-z0-9]+)?$")]
    private static partial Regex PlatformRegex { get; }
}
