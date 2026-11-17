// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

// Every value is explicit: a repo generates its manifest along with its Dockerfiles, so there are
// no defaults, variables or inheritance to resolve. The one exception is "$(Repo:<key>)" in build
// args, which ImageBuilder resolves at build time to wherever that repo's images are being built.
public sealed record Manifest
{
    // The registry images are published to, like "mcr.microsoft.com".
    public string? Registry { get; set; }
    public Readme? Readme { get; set; }
    public required Dictionary<string, Repo> Repos { get; set; }
}

public sealed record Repo
{
    public required string Name { get; set; }
    public string? McrTagsMetadataTemplate { get; set; }
    public Readme[] Readmes { get; set; } = [];
    // Keys stay the same across version updates, like "10.0-noble".
    public required Dictionary<string, Image> Images { get; set; }
}

public sealed record Image
{
    // Tags that point to every platform of the image.
    public string[] SharedTags { get; set; } = [];
    // Keyed by OCI platform, like "linux/amd64" or "linux/arm/v7".
    public required Dictionary<string, Platform> Platforms { get; set; }
}

public sealed record Platform
{
    // Relative to the manifest. The Dockerfile's directory is the build context.
    public required string DockerfilePath { get; set; }
    public Dictionary<string, string> BuildArgs { get; set; } = [];
    public string[] Tags { get; set; } = [];
    // Published like Tags, but left out of readmes and MCR tag metadata.
    public string[] UndocumentedTags { get; set; } = [];
    // Labels the build host must have, like "windowsservercore-ltsc2025".
    public string[] Constraints { get; set; } = [];
}

public sealed record Readme
{
    public required string Path { get; set; }
    public string? TemplatePath { get; set; }
}
