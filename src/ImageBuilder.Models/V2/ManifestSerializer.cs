// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public static class ManifestSerializer
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

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

    public static string Serialize(Manifest manifest) => JsonSerializer.Serialize(manifest, JsonOptions);

    private static List<string> Validate(Manifest manifest)
    {
        List<string> errors = [];

        IEnumerable<Image> images = manifest.Repos.Values.SelectMany(repo => repo.Images.Values);

        IEnumerable<string> keyParts = manifest.Repos.Keys
            .Concat(manifest.Repos.Values.SelectMany(repo => repo.Images.Keys))
            .Concat(images.SelectMany(image => image.Platforms.Keys));

        IEnumerable<string> invalidKeyParts = keyParts
            .Where(keyPart => !KeyPatterns.Part.IsMatch(keyPart))
            .Distinct();

        foreach (string keyPart in invalidKeyParts)
        {
            errors.Add($"Key '{keyPart}' must be {KeyPatterns.Description}.");
        }

        IEnumerable<IGrouping<string, Repo>> reposByName = manifest.Repos.Values
            .GroupBy(repo => repo.Name);

        foreach (IGrouping<string, Repo> reposWithName in reposByName)
        {
            IEnumerable<string> duplicateTags = reposWithName
                .SelectMany(repo => repo.Images.Values)
                .SelectMany(GetTags)
                .GroupBy(tag => tag)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (string tag in duplicateTags)
            {
                errors.Add($"Tag '{reposWithName.Key}:{tag}' is listed more than once.");
            }
        }

        return errors;
    }

    private static IEnumerable<string> GetTags(Image image) =>
        image.Platforms.Values
            .SelectMany(platform => platform.Tags)
            .Concat(image.SharedTags);
}
