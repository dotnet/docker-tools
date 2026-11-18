// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

public sealed record ImageEntry(Manifest Manifest, string RepoKey, Repo Repo, string ImageKey, Image Image);

public sealed record PlatformEntry(ImageEntry ImageEntry, string PlatformKey, ImagePlatform ImagePlatform);
