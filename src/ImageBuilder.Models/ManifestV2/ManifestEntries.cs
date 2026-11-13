// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// An image in a manifest, with the repo and group it belongs to.
/// </summary>
/// <param name="Manifest">The manifest containing the image.</param>
/// <param name="RepoKey">The key of the repo containing the image.</param>
/// <param name="Repo">The repo containing the image.</param>
/// <param name="GroupKey">The key of the group containing the image, or null if it isn't in a group.</param>
/// <param name="Group">The group containing the image, or null if it isn't in a group.</param>
/// <param name="ImageKey">The image's key within its repo or group.</param>
/// <param name="Image">The image.</param>
public sealed record ImageEntry(
    Manifest Manifest,
    string RepoKey,
    Repo Repo,
    string? GroupKey,
    Group? Group,
    string ImageKey,
    Image Image)
{
    /// <summary>
    /// Separates the parts of image and platform keys.
    /// </summary>
    public const string KeySeparator = "__";

    /// <summary>
    /// The image's key, unique within the manifest, like "sdk__10.0__noble".
    /// </summary>
    public string Key => GroupKey is null
        ? string.Join(KeySeparator, RepoKey, ImageKey)
        : string.Join(KeySeparator, RepoKey, GroupKey, ImageKey);

    /// <summary>
    /// The image's full reference for <paramref name="tag"/>, like "mcr.microsoft.com/dotnet/sdk:10.0".
    /// </summary>
    public string GetReference(string tag) =>
        Manifest.Registry is null ? $"{Repo.Name}:{tag}" : $"{Manifest.Registry}/{Repo.Name}:{tag}";

    /// <summary>
    /// Every platform of the image.
    /// </summary>
    public IEnumerable<PlatformEntry> GetPlatforms()
    {
        foreach ((string platformKey, Platform platform) in Image.Platforms)
        {
            yield return new PlatformEntry(this, platformKey, platform);
        }
    }
}

/// <summary>
/// A platform in a manifest, with the image it belongs to.
/// </summary>
/// <param name="ImageEntry">The image containing the platform.</param>
/// <param name="PlatformKey">The platform's key within its image.</param>
/// <param name="Platform">The platform.</param>
public sealed record PlatformEntry(ImageEntry ImageEntry, string PlatformKey, Platform Platform)
{
    /// <summary>
    /// The platform's key, unique within the manifest, like "sdk__10.0__noble__amd64".
    /// </summary>
    public string Key => string.Join(ImageEntry.KeySeparator, ImageEntry.Key, PlatformKey);

    /// <summary>
    /// The values the platform's placeholders and Dockerfile template can refer to: its metadata merged
    /// from the manifest down to the platform, where the closest declaration of each key wins, plus the
    /// built-in "repoKey", "os" and "arch".
    /// </summary>
    public Dictionary<string, JsonElement> GetVariables()
    {
        Dictionary<string, JsonElement> variables = new(StringComparer.Ordinal);
        Dictionary<string, JsonElement>[] scopes =
        [
            ImageEntry.Manifest.Metadata,
            ImageEntry.Repo.Metadata,
            ImageEntry.Group?.Metadata ?? [],
            ImageEntry.Image.Metadata,
            Platform.Metadata
        ];
        foreach (Dictionary<string, JsonElement> scope in scopes)
        {
            foreach ((string key, JsonElement value) in scope)
            {
                variables[key] = value;
            }
        }

        JsonElement builtIns = BuiltInVariables.Serialize(ImageEntry.RepoKey, Platform);
        foreach (JsonProperty builtIn in builtIns.EnumerateObject())
        {
            variables.Add(builtIn.Name, builtIn.Value);
        }

        return variables;
    }
}
