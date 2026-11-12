// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

namespace TemplateTool;

internal static class ManifestExtensions
{
    /// <summary>
    /// Every image in the manifest with its repo, keyed by its repo, group (if any) and image keys,
    /// like "sdk__10.0__noble".
    /// </summary>
    public static IEnumerable<(string Key, Repo Repo, Image Image)> GetImages(this Manifest manifest)
    {
        foreach ((string repoKey, Repo repo) in manifest.Repos)
        {
            foreach ((string imageKey, Image image) in repo.Images)
            {
                yield return ($"{repoKey}__{imageKey}", repo, image);
            }

            foreach ((string groupKey, Group group) in repo.Groups)
            {
                foreach ((string imageKey, Image image) in group.Images)
                {
                    yield return ($"{repoKey}__{groupKey}__{imageKey}", repo, image);
                }
            }
        }
    }
}
