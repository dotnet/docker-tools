// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// Queries over a loaded manifest.
/// </summary>
public static class ManifestExtensions
{
    /// <summary>
    /// Every image in the manifest, in repo order, with ungrouped images before grouped ones.
    /// </summary>
    public static IEnumerable<ImageEntry> GetImages(this Manifest manifest)
    {
        foreach ((string repoKey, Repo repo) in manifest.Repos)
        {
            foreach ((string imageKey, Image image) in repo.Images)
            {
                yield return new ImageEntry(manifest, repoKey, repo, null, null, imageKey, image);
            }

            foreach ((string groupKey, Group group) in repo.Groups)
            {
                foreach ((string imageKey, Image image) in group.Images)
                {
                    yield return new ImageEntry(manifest, repoKey, repo, groupKey, group, imageKey, image);
                }
            }
        }
    }

    /// <summary>
    /// Every platform in the manifest, in the same order as <see cref="GetImages"/>.
    /// </summary>
    public static IEnumerable<PlatformEntry> GetPlatforms(this Manifest manifest) =>
        manifest.GetImages().SelectMany(image => image.GetPlatforms());
}
