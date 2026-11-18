// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

public sealed record ImageArtifactDetails
{
    public ImageManifest[] Manifests { get; set; } = [];
    public ImageIndex[] Indexes { get; set; } = [];
}

public sealed record ImageManifest
{
    public required string Key { get; set; }
    public required string Digest { get; set; }
    public required string Os { get; set; }
    public required string Architecture { get; set; }
    public string? Variant { get; set; }
    public string? OsVersion { get; set; }
    public required DateTime Created { get; set; }
    public string[] Tags { get; set; } = [];
    public required string CommitUrl { get; set; }
    public Dictionary<string, string> BaseImages { get; set; } = [];
    public Layer[] Layers { get; set; } = [];
}

public sealed record ImageIndex
{
    public required string Key { get; set; }
    public required string Digest { get; set; }
    public required DateTime Created { get; set; }
    public string[] Tags { get; set; } = [];
    public required string[] Manifests { get; set; }
}

public sealed record Layer
{
    public required string Digest { get; set; }
    public required long Size { get; set; }
}
