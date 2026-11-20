// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public static class ManifestExtensions
{
    extension(Manifest manifest)
    {
        public IEnumerable<ImageKey> GetImageKeys() =>
            manifest.Repos.SelectMany(repo =>
                repo.Value.Images.Keys.Select(image => new ImageKey(repo.Key, image)));

        public Image GetImage(ImageKey key) => manifest.Repos[key.Repo].Images[key.Image];
    }
}
