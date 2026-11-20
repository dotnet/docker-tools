// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2.Projections;

public static class ProjectionExtensions
{
    extension(Manifest manifest)
    {
        public IEnumerable<ImageEntry> GetAllImageEntries() =>
            manifest
                .GetImageKeys()
                .Select(key => new ImageEntry(
                    Key: key,
                    Registry: manifest.Registry,
                    Repo: manifest.Repos[key.Repo],
                    Image: manifest.GetImage(key)));

        public IEnumerable<PlatformEntry> GetAllPlatformEntries() =>
            manifest
                .GetAllImageEntries()
                .SelectMany(imageEntry => imageEntry.GetPlatformEntries());
    }

    extension(ImageEntry entry)
    {
        public IEnumerable<ImageReference> GetSharedReferences() =>
            entry.Image.SharedTags.Select(tag => new ImageReference(entry.Registry, entry.Repo.Name, tag));

        public IEnumerable<PlatformEntry> GetPlatformEntries() =>
            entry.Image.Platforms.Select(platform => new PlatformEntry(
                Key: new PlatformKey(entry.Key.Repo, entry.Key.Image, platform.Key),
                Registry: entry.Registry,
                Repo: entry.Repo,
                Image: entry.Image,
                ImagePlatform: platform.Value));
    }

    extension(PlatformEntry entry)
    {
        public IEnumerable<ImageReference> GetPlatformReferences() =>
            entry.ImagePlatform.Tags.Select(tag => new ImageReference(entry.Registry, entry.Repo.Name, tag));
    }
}
