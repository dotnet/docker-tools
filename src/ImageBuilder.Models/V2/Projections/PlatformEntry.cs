// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2.Projections;

public sealed record PlatformEntry(
    PlatformKey Key,
    string? Registry,
    Repo Repo,
    Image Image,
    ImagePlatform ImagePlatform);
