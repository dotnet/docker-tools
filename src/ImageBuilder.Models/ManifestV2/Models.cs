// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

public sealed record Manifest
{
    public Readme? Readme { get; init; }
    public Dictionary<string, OsDefinition> OsDefinitions { get; init; } = [];
    public Dictionary<string, Repo> Repos { get; init; } = [];
    public TagPatterns TagPatterns { get; init; } = new();
    public Dictionary<string, JsonElement> Metadata { get; init; } = [];
}

public sealed record Repo
{
    public required string Name { get; init; }
    public string? McrTagsMetadataTemplate { get; init; }
    public Dictionary<string, Image> Images { get; init; } = [];
    public Readme[] Readmes { get; init; } = [];
    public TagPatterns TagPatterns { get; init; } = new();
    public Dictionary<string, JsonElement> Metadata { get; init; } = [];
}

public sealed record Image
{
    public string? ProductVersion { get; init; }
    public string? Syndication { get; init; }
    public Dictionary<string, Platform> Platforms { get; init; } = [];
    public Tag[] Tags { get; init; } = [];
    public TagPatterns TagPatterns { get; init; } = new();
    public Dictionary<string, JsonElement> Metadata { get; init; } = [];
}

public sealed record Platform
{
    public required TargetPlatform TargetPlatform { get; init; }
    public required string Dockerfile { get; init; }
    public string? DockerfileTemplate { get; init; }
    public Dictionary<string, string> BuildArgs { get; init; } = [];
    public Tag[] Tags { get; init; } = [];
    public Dictionary<string, JsonElement> Metadata { get; init; } = [];
}

public sealed record TargetPlatform(
    string OsDefinition,
    Arch Arch);

public sealed record OsDefinition(
    OS OS,
    string Version,
    string TagPart);

public sealed record TagPatterns
{
    public Tag[] ImageTags { get; init; } = [];
    public Tag[] PlatformTags { get; init; } = [];
}

public sealed record Tag(
    string Name,
    bool Undocumented = false);

public sealed record Readme(
    string Path,
    string? TemplatePath = null);

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
