// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public static class ImageArtifactDetailsExtensions
{
    extension(ImageArtifactDetails details)
    {
        public IEnumerable<KeyValuePair<string, ImageManifest>> GetManifests(ImageKey imageKey) =>
            details.Manifests.Where(manifest => manifest.Key.StartsWith($"{imageKey}{KeyPatterns.Separator}", StringComparison.Ordinal));
    }
}
