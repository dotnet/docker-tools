// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// Queries over a loaded manifest.
/// </summary>
public static class ManifestExtensions
{
    /// <summary>
    /// Every image in the manifest, in manifest order.
    /// </summary>
    public static IEnumerable<ImageEntry> GetImages(this Manifest manifest)
    {
        foreach ((string repoKey, Repo repo) in manifest.Repos)
        {
            foreach ((string imageKey, Image image) in repo.Images)
            {
                yield return new ImageEntry(manifest, repoKey, repo, imageKey, image);
            }
        }
    }

    /// <summary>
    /// Every platform in the manifest, in the same order as <see cref="GetImages"/>.
    /// </summary>
    public static IEnumerable<PlatformEntry> GetPlatforms(this Manifest manifest)
    {
        foreach (ImageEntry image in manifest.GetImages())
        {
            foreach (PlatformEntry platform in image.GetPlatforms())
            {
                yield return platform;
            }
        }
    }
}
