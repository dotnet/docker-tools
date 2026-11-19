// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

/// <summary>
/// Reads and writes JSON image artifact details.
/// </summary>
public static partial class ImageArtifactDetailsSerializer
{
    /// <summary>
    /// Reads and validates the image artifact details at <paramref name="filePath"/>.
    /// </summary>
    /// <exception cref="JsonException">The image artifact details are not valid JSON or do not match the model.</exception>
    /// <exception cref="InvalidDataException">The image artifact details are invalid.</exception>
    public static ImageArtifactDetails Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        ImageArtifactDetails details = JsonSerializer.Deserialize<ImageArtifactDetails>(json, ManifestSerializer.JsonOptions)
            ?? throw new InvalidDataException($"{filePath}: the image artifact details must not be null.");

        Validate(details);
        return details;
    }

    /// <summary>
    /// Writes <paramref name="details"/> as JSON, leaving out empty and unset values.
    /// </summary>
    public static string Serialize(ImageArtifactDetails details) =>
        JsonSerializer.Serialize(details, ManifestSerializer.JsonOptions);

    private static void Validate(ImageArtifactDetails details)
    {
        HashSet<string> manifestKeys = new(StringComparer.Ordinal);
        foreach (ImageManifest manifest in details.Manifests)
        {
            if (!manifestKeys.Add(manifest.Key))
            {
                throw new InvalidDataException($"{manifest.Key}: more than one manifest has this key.");
            }

            if (!KeyPatterns.PlatformKey.IsMatch(manifest.Key))
            {
                throw new InvalidDataException(
                    $"{manifest.Key}: a manifest key must be 'repo{KeyPatterns.Separator}image{KeyPatterns.Separator}platform', each part {KeyPatterns.Description}.");
            }

            ValidateDigests(manifest.Key, [manifest.Digest]);
            if (manifest.BaseImage is not null)
            {
                ValidateDigests(manifest.Key, [manifest.BaseImage.Digest]);
            }
        }

        HashSet<string> indexKeys = new(StringComparer.Ordinal);
        foreach (ImageIndex index in details.Indexes)
        {
            if (!indexKeys.Add(index.Key))
            {
                throw new InvalidDataException($"{index.Key}: more than one index has this key.");
            }

            if (!KeyPatterns.ImageKey.IsMatch(index.Key))
            {
                throw new InvalidDataException(
                    $"{index.Key}: an index key must be 'repo{KeyPatterns.Separator}image', each part {KeyPatterns.Description}.");
            }

            ValidateDigests(index.Key, [index.Digest, .. index.Manifests]);

            string[] manifestDigests = details
                .GetManifests(index)
                .Select(manifest => manifest.Digest)
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (!index.Manifests.Order(StringComparer.Ordinal).SequenceEqual(manifestDigests))
            {
                throw new InvalidDataException(
                    $"{index.Key}: the index lists [{string.Join(", ", index.Manifests)}]"
                    + $" but the manifests for {index.Key} are [{string.Join(", ", manifestDigests)}].");
            }
        }
    }

    private static void ValidateDigests(string owner, IEnumerable<string> digests)
    {
        foreach (string digest in digests)
        {
            if (!DigestRegex.IsMatch(digest))
            {
                throw new InvalidDataException($"{owner}: '{digest}' is not a digest like 'algorithm:encoded'.");
            }
        }
    }

    [GeneratedRegex(@"^[a-z0-9]+(?:[+._-][a-z0-9]+)*:[a-zA-Z0-9=_-]+$")]
    private static partial Regex DigestRegex { get; }
}
