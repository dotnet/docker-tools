// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// Reads and writes JSON image artifact details.
/// </summary>
public static partial class ImageArtifactDetailsSerializer
{
    /// <summary>
    /// Reads and validates the image artifact details at <paramref name="filePath"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">The image artifact details are invalid.</exception>
    public static ImageArtifactDetails Load(string filePath)
    {
        ImageArtifactDetails details;
        try
        {
            details = JsonSerializer.Deserialize<ImageArtifactDetails>(File.ReadAllText(filePath), ManifestSerializer.JsonOptions)
                ?? throw new InvalidDataException($"{filePath}: the image artifact details must not be null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{filePath}: {exception.Message}", exception);
        }

        Validate(details);
        return details;
    }

    /// <summary>
    /// Writes <paramref name="details"/> as JSON, leaving out empty and unset values.
    /// </summary>
    public static string Serialize(ImageArtifactDetails details) =>
        JsonSerializer.Serialize(details, ManifestSerializer.JsonOptions) + "\n";

    private static void Validate(ImageArtifactDetails details)
    {
        HashSet<string> manifestKeys = new(StringComparer.Ordinal);
        foreach (ImageManifest manifest in details.Manifests)
        {
            if (!manifestKeys.Add(manifest.Key))
            {
                throw new InvalidDataException($"{manifest.Key}: more than one manifest has this key.");
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
                throw new InvalidDataException($"{owner}: '{digest}' is not a digest like 'sha256:<hex>'.");
            }
        }
    }

    [GeneratedRegex(@"^[a-z0-9]+(?:[+._-][a-z0-9]+)*:[a-zA-Z0-9=_-]+$")]
    private static partial Regex DigestRegex { get; }
}
