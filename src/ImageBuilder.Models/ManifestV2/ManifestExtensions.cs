// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

public static class ManifestExtensions
{
    extension(Manifest manifest)
    {
        public IEnumerable<ImageKey> GetImageKeys() =>
            manifest.Repos.SelectMany(repo => repo.Value.Images.Keys.Select(image => new ImageKey(repo.Key, image)));

        public IEnumerable<PlatformKey> GetPlatformKeys() =>
            manifest.GetImageKeys().SelectMany(imageKey =>
                manifest.Get(imageKey).Platforms.Keys.Select(platform => new PlatformKey(imageKey.Repo, imageKey.Image, platform)));

        public Image Get(ImageKey key) => manifest.Repos[key.Repo].Images[key.Image];

        public ImagePlatform Get(PlatformKey key) => manifest.Get(key.ImageKey).Platforms[key.Platform];

        public string GetReference(ImageKey key, string tag)
        {
            string repoName = manifest.Repos[key.Repo].Name;
            return manifest.Registry is null ? $"{repoName}:{tag}" : $"{manifest.Registry}/{repoName}:{tag}";
        }
    }

    extension(PlatformKey key)
    {
        public ImageKey ImageKey => new(key.Repo, key.Image);
    }
}
