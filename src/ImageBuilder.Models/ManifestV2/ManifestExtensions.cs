// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

public static class ManifestExtensions
{
    private const string KeySeparator = "__";

    extension(Manifest manifest)
    {
        public IEnumerable<ImageEntry> GetImages()
        {
            foreach ((string repoKey, Repo repo) in manifest.Repos)
            {
                foreach ((string imageKey, Image image) in repo.Images)
                {
                    yield return new ImageEntry(manifest, repoKey, repo, imageKey, image);
                }
            }
        }

        public IEnumerable<PlatformEntry> GetPlatforms() =>
            manifest.GetImages().SelectMany(imageEntry => imageEntry.GetPlatforms());
    }

    extension(ImageEntry imageEntry)
    {
        public string Key => string.Join(KeySeparator, imageEntry.RepoKey, imageEntry.ImageKey);

        public string GetReference(string tag) => imageEntry.Manifest.Registry is null
            ? $"{imageEntry.Repo.Name}:{tag}"
            : $"{imageEntry.Manifest.Registry}/{imageEntry.Repo.Name}:{tag}";

        public IEnumerable<PlatformEntry> GetPlatforms() =>
            imageEntry.Image.Platforms.Select(platform => new PlatformEntry(imageEntry, platform.Key, platform.Value));
    }

    extension(PlatformEntry platformEntry)
    {
        public string Key => string.Join(KeySeparator, platformEntry.ImageEntry.Key, platformEntry.PlatformKey);
    }
}
