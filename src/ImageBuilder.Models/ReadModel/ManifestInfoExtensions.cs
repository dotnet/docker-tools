// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

public static class ManifestInfoExtensions
{
    extension(ManifestInfo manifest)
    {
        public IEnumerable<ImageInfo> AllImages => manifest.Repos.SelectMany(repo => repo.Images);
        public IEnumerable<PlatformInfo> AllPlatforms => manifest.AllImages.SelectMany(image => image.Platforms);
    }
}
