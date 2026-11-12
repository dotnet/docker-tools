// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

public sealed record Manifest
{
    // The registry images are published to, like "mcr.microsoft.com".
    public string? Registry { get; set; }
    public Readme? Readme { get; set; }
    public ManifestDefaults? Defaults { get; set; }
    public Dictionary<string, Repo> Repos { get; set; } = [];
    public Dictionary<string, JsonElement> Metadata { get; set; } = [];
}

public sealed record Repo
{
    public required string Name { get; set; }
    public string? McrTagsMetadataTemplate { get; set; }
    public ManifestDefaults? Defaults { get; set; }
    public Dictionary<string, Image> Images { get; set; } = [];
    public Dictionary<string, Group> Groups { get; set; } = [];
    public Readme[] Readmes { get; set; } = [];
    public Dictionary<string, JsonElement> Metadata { get; set; } = [];
}

// An optional level between a repo and its images that shares metadata and defaults.
public sealed record Group
{
    public ManifestDefaults? Defaults { get; set; }
    public Dictionary<string, Image> Images { get; set; } = [];
    public Dictionary<string, JsonElement> Metadata { get; set; } = [];
}

public sealed record Image
{
    public string? Syndication { get; set; }
    public ManifestDefaults? Defaults { get; set; }
    public Dictionary<string, Platform> Platforms { get; set; } = [];
    public Dictionary<string, Tag>? Tags { get; set; }
    public Dictionary<string, JsonElement> Metadata { get; set; } = [];
}

public sealed record Platform
{
    public required OSRelease OS { get; set; }
    public required Arch Arch { get; set; }
    public string? Dockerfile { get; set; }
    public string? DockerfileTemplate { get; set; }
    public Dictionary<string, string> BuildArgs { get; set; } = [];
    public Dictionary<string, Tag>? Tags { get; set; }
    public Dictionary<string, JsonElement> Metadata { get; set; } = [];
}

public sealed record ManifestDefaults
{
    public ImageDefaults Image { get; set; } = new();
    public PlatformDefaults Platform { get; set; } = new();
}

public sealed record ImageDefaults
{
    public Dictionary<string, Tag>? Tags { get; set; }
}

public sealed record PlatformDefaults
{
    public string? Dockerfile { get; set; }
    public string? DockerfileTemplate { get; set; }
    public Dictionary<string, string>? BuildArgs { get; set; }
    public Dictionary<string, Tag>? Tags { get; set; }
}

public sealed record OSRelease
{
    // Docker platform and OS-specific build behavior.
    public required OSFamily Family { get; set; }
    // Identifies the OS in image info, filters, tags and paths, like "noble".
    public required string Name { get; set; }
    // Shown in readmes and MCR tag metadata, like "Ubuntu 24.04".
    public required string DisplayName { get; set; }
    // The OS's own version, like "24.04". Only Dockerfile templates read it.
    public required string Version { get; set; }
}

public sealed record Tag
{
    public bool Undocumented { get; set; }
}

public sealed record Readme
{
    public required string Path { get; set; }
    public string? TemplatePath { get; set; }
}

public enum Arch
{
    Amd64,
    Arm32v7,
    Arm64v8,
}

public enum OSFamily
{
    Linux,
    Windows,
}
