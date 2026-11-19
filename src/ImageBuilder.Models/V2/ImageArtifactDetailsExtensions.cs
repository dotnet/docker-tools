// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public static class ImageArtifactDetailsExtensions
{
    extension(ImageArtifactDetails details)
    {
        public IEnumerable<ImageManifest> GetManifests(ImageIndex index) =>
            details.Manifests.Where(manifest => manifest.Key.StartsWith($"{index.Key}{KeyPatterns.Separator}", StringComparison.Ordinal));
    }
}
