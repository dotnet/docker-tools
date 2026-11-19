// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public static class ImageArtifactDetailsSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = ManifestSerializer.JsonOptions;

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

    public static string Serialize(ImageArtifactDetails details) => JsonSerializer.Serialize(details, JsonOptions);

    private static List<string> Validate(ImageArtifactDetails details)
    {
        List<string> errors = [];

        HashSet<string> manifestDigests = details.Manifests.Values
            .Select(manifest => manifest.Digest)
            .ToHashSet(StringComparer.Ordinal);

        foreach ((string key, ImageIndex index) in details.Indexes)
        {
            IEnumerable<string> unknownDigests = index.Manifests
                .Where(digest => !manifestDigests.Contains(digest));

            foreach (string digest in unknownDigests)
            {
                errors.Add($"{nameof(ImageIndex)} '{key}' lists '{digest}', which is not the digest of any {nameof(ImageManifest)}.");
            }
        }

        return errors;
    }
}
