// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// An image in a manifest, with the repo it belongs to.
/// </summary>
/// <param name="Manifest">The manifest containing the image.</param>
/// <param name="RepoKey">The key of the repo containing the image.</param>
/// <param name="Repo">The repo containing the image.</param>
/// <param name="ImageKey">The image's key within its repo.</param>
/// <param name="Image">The image.</param>
public sealed record ImageEntry(Manifest Manifest, string RepoKey, Repo Repo, string ImageKey, Image Image)
{
    /// <summary>
    /// Separates the parts of image and platform keys.
    /// </summary>
    public const string KeySeparator = "__";

    /// <summary>
    /// The image's key, unique within the manifest and stable across version updates, like "sdk__10.0-noble".
    /// </summary>
    public string Key => string.Join(KeySeparator, RepoKey, ImageKey);

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
/// <param name="PlatformKey">The platform's OCI platform within its image, like "linux/amd64".</param>
/// <param name="Platform">The platform.</param>
public sealed record PlatformEntry(ImageEntry ImageEntry, string PlatformKey, Platform Platform)
{
    /// <summary>
    /// The platform's key, unique within the manifest and stable across version updates,
    /// like "sdk__10.0-noble__linux/amd64".
    /// </summary>
    public string Key => string.Join(ImageEntry.KeySeparator, ImageEntry.Key, PlatformKey);
}
