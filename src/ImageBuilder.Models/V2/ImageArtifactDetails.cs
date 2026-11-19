// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public sealed record ImageArtifactDetails
{
    public Dictionary<string, ImageManifest> Manifests { get; set; } = [];
    public Dictionary<string, ImageIndex> Indexes { get; set; } = [];
}

public sealed record ImageManifest
{
    public required string Registry { get; set; }
    public required string Repository { get; set; }
    public required string Digest { get; set; }
    public string[] Tags { get; set; } = [];
    public required string CommitUrl { get; set; }
    public BaseImage? BaseImage { get; set; }
}

public sealed record ImageIndex
{
    public required string Registry { get; set; }
    public required string Repository { get; set; }
    public required string Digest { get; set; }
    public string[] Tags { get; set; } = [];
    public required string[] Manifests { get; set; }
}

public sealed record BaseImage
{
    public required string Image { get; set; }
    public required string Digest { get; set; }
}
