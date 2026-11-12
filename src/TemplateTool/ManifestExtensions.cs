// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

namespace TemplateTool;

internal static class ManifestExtensions
{
    /// <summary>
    /// Every image in the manifest, keyed by its repo, group (if any) and image keys, like "sdk__10.0__noble".
    /// </summary>
    public static IEnumerable<(string Key, Image Image)> GetImages(this Manifest manifest) =>
        manifest.Repos.SelectMany(repo =>
            repo.Value.Images.Select(image => ($"{repo.Key}__{image.Key}", image.Value))
                .Concat(repo.Value.Groups.SelectMany(group => group.Value.Images.Select(
                    image => ($"{repo.Key}__{group.Key}__{image.Key}", image.Value)))));
}
