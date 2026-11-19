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
    private const string DigestDescription = "an OCI digest like 'algorithm:encoded'";

    private static readonly JsonSerializerOptions JsonOptions = ManifestSerializer.JsonOptions;

    /// <summary>
    /// Reads and validates the image artifact details at <paramref name="filePath"/>.
    /// </summary>
    /// <exception cref="JsonException">The image artifact details are not valid JSON or do not match the model.</exception>
    /// <exception cref="InvalidDataException">The image artifact details are invalid. The message lists every problem.</exception>
    public static ImageArtifactDetails Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        ImageArtifactDetails details = JsonSerializer.Deserialize<ImageArtifactDetails>(json, JsonOptions)
            ?? throw new InvalidDataException($"{filePath}: the image artifact details must not be null.");

        List<string> errors = Validate(details);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"{filePath} has {errors.Count} error(s):{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        return details;
    }

    /// <summary>
    /// Writes <paramref name="details"/> as JSON, leaving out empty and unset values.
    /// </summary>
    public static string Serialize(ImageArtifactDetails details) => JsonSerializer.Serialize(details, JsonOptions);

    private static List<string> Validate(ImageArtifactDetails details)
    {
        List<string> errors = [];

        foreach ((string key, ImageManifest manifest) in details.Manifests)
        {
            if (!KeyPatterns.PlatformKey.IsMatch(key))
            {
                errors.Add(
                    $"{nameof(ImageManifest)} '{key}': the key must be 'repo{KeyPatterns.Separator}image{KeyPatterns.Separator}platform',"
                    + $" each part {KeyPatterns.Description}.");
            }

            if (!DigestRegex.IsMatch(manifest.Digest))
            {
                errors.Add($"{nameof(ImageManifest)} '{key}': digest '{manifest.Digest}' is not {DigestDescription}.");
            }

            if (manifest.BaseImage is not null && !DigestRegex.IsMatch(manifest.BaseImage.Digest))
            {
                errors.Add($"{nameof(ImageManifest)} '{key}': base image digest '{manifest.BaseImage.Digest}' is not {DigestDescription}.");
            }
        }

        foreach ((string key, ImageIndex index) in details.Indexes)
        {
            if (!DigestRegex.IsMatch(index.Digest))
            {
                errors.Add($"{nameof(ImageIndex)} '{key}': digest '{index.Digest}' is not {DigestDescription}.");
            }

            foreach (string digest in index.Manifests.Where(digest => !DigestRegex.IsMatch(digest)))
            {
                errors.Add($"{nameof(ImageIndex)} '{key}': manifest digest '{digest}' is not {DigestDescription}.");
            }

            IEnumerable<string> repeatedDigests = index.Manifests
                .GroupBy(digest => digest, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (string digest in repeatedDigests)
            {
                errors.Add($"{nameof(ImageIndex)} '{key}': manifest digest '{digest}' is listed more than once.");
            }

            Match match = KeyPatterns.ImageKey.Match(key);
            if (!match.Success)
            {
                errors.Add(
                    $"{nameof(ImageIndex)} '{key}': the key must be 'repo{KeyPatterns.Separator}image', each part {KeyPatterns.Description}.");
                continue;
            }

            ImageKey imageKey = new(match.Groups["repo"].Value, match.Groups["image"].Value);
            KeyValuePair<string, ImageManifest>[] imageManifests = details.GetManifests(imageKey).ToArray();

            IEnumerable<KeyValuePair<string, ImageManifest>> unlistedManifests = imageManifests
                .Where(pair => !index.Manifests.Contains(pair.Value.Digest, StringComparer.Ordinal));

            foreach ((string manifestKey, ImageManifest manifest) in unlistedManifests)
            {
                errors.Add($"{nameof(ImageIndex)} '{key}': does not list manifest '{manifestKey}' ({manifest.Digest}).");
            }

            HashSet<string> imageManifestDigests = imageManifests
                .Select(pair => pair.Value.Digest)
                .ToHashSet(StringComparer.Ordinal);

            IEnumerable<string> unknownDigests = index.Manifests
                .Where(digest => !imageManifestDigests.Contains(digest))
                .Distinct(StringComparer.Ordinal);

            foreach (string digest in unknownDigests)
            {
                errors.Add($"{nameof(ImageIndex)} '{key}': lists '{digest}', which is not the digest of any manifest of image '{imageKey}'.");
            }
        }

        return errors;
    }

    [GeneratedRegex(@"^[a-z0-9]+(?:[+._-][a-z0-9]+)*:[a-zA-Z0-9=_-]+$")]
    private static partial Regex DigestRegex { get; }
}
