// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

public sealed record Manifest
{
    public Readme? Readme { get; set; }
    public Dictionary<string, OsDefinition> OsDefinitions { get; set; } = [];
    public Dictionary<string, Repo> Repos { get; set; } = [];
    public TagPatterns TagPatterns { get; set; } = new();
    public Dictionary<string, JsonElement> Metadata { get; set; } = [];
}

public sealed record Repo
{
    public required string Name { get; set; }
    public string? McrTagsMetadataTemplate { get; set; }
    public Dictionary<string, Image> Images { get; set; } = [];
    public Readme[] Readmes { get; set; } = [];
    public TagPatterns TagPatterns { get; set; } = new();
    public Dictionary<string, JsonElement> Metadata { get; set; } = [];
}

public sealed record Image
{
    public string? ProductVersion { get; set; }
    public string? Syndication { get; set; }
    public Dictionary<string, Platform> Platforms { get; set; } = [];
    public Tag[] Tags { get; set; } = [];
    public TagPatterns TagPatterns { get; set; } = new();
    public Dictionary<string, JsonElement> Metadata { get; set; } = [];
}

public sealed record Platform
{
    public required TargetPlatform TargetPlatform { get; set; }
    public required string Dockerfile { get; set; }
    public string? DockerfileTemplate { get; set; }
    public Dictionary<string, string> BuildArgs { get; set; } = [];
    public Tag[] Tags { get; set; } = [];
    public Dictionary<string, JsonElement> Metadata { get; set; } = [];
}

public sealed record TargetPlatform
{
    public required string OsDefinition { get; set; }
    public required Arch Arch { get; set; }
}

public sealed record OsDefinition
{
    public required OS OS { get; set; }
    public required string Version { get; set; }
    public required string TagPart { get; set; }
}

public sealed record TagPatterns
{
    public Tag[] ImageTags { get; set; } = [];
    public Tag[] PlatformTags { get; set; } = [];
}

public sealed record Tag
{
    public required string Name { get; set; }
    public bool Undocumented { get; set; }
}

public sealed record Readme
{
    public required string Path { get; set; }
    public string? TemplatePath { get; set; }
}

public enum Arch
{
    Arm32,
    Arm64,
    Amd64,
}

public enum OS
{
    Linux,
    Windows,
}
