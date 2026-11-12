// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

namespace TemplateTool;

internal static class ManifestExtensions
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
                yield return new ImageEntry($"{repoKey}__{imageKey}", repo, image);
            }

            foreach ((string groupKey, Group group) in repo.Groups)
            {
                foreach ((string imageKey, Image image) in group.Images)
                {
                    yield return new ImageEntry($"{repoKey}__{groupKey}__{imageKey}", repo, image);
                }
            }
        }
    }
}
