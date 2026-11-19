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
        foreach ((string key, ImageManifest manifest) in details.Manifests)
        {
            if (!KeyPatterns.PlatformKey.IsMatch(key))
            {
                throw new InvalidDataException(
                    $"{key}: a manifest key must be 'repo{KeyPatterns.Separator}image{KeyPatterns.Separator}platform', each part {KeyPatterns.Description}.");
            }

            ValidateDigests(key, [manifest.Digest]);
            if (manifest.BaseImage is not null)
            {
                ValidateDigests(key, [manifest.BaseImage.Digest]);
            }
        }

        foreach ((string key, ImageIndex index) in details.Indexes)
        {
            Match match = KeyPatterns.ImageKey.Match(key);
            if (!match.Success)
            {
                throw new InvalidDataException(
                    $"{key}: an index key must be 'repo{KeyPatterns.Separator}image', each part {KeyPatterns.Description}.");
            }

            ValidateDigests(key, [index.Digest, .. index.Manifests]);

            string[] manifestDigests = details
                .GetManifests(new ImageKey(match.Groups["repo"].Value, match.Groups["image"].Value))
                .Select(manifest => manifest.Value.Digest)
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (!index.Manifests.Order(StringComparer.Ordinal).SequenceEqual(manifestDigests))
            {
                throw new InvalidDataException(
                    $"{key}: the index lists [{string.Join(", ", index.Manifests)}]"
                    + $" but the manifests for {key} are [{string.Join(", ", manifestDigests)}].");
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
