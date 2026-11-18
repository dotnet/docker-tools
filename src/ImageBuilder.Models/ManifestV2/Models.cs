// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using YamlDotNet.Serialization;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

public sealed record Manifest
{
    public string? Registry { get; set; }
    public Readme? Readme { get; set; }
    public required Dictionary<string, Repo> Repos { get; set; }
}

public sealed record Repo
{
    public required string Name { get; set; }
    public string? McrTagsMetadataTemplate { get; set; }
    public Readme[] Readmes { get; set; } = [];
    public required Dictionary<string, Image> Images { get; set; }
}

public sealed record Image
{
    public string[] SharedTags { get; set; } = [];
    public required Dictionary<string, Platform> Platforms { get; set; }
}

public sealed record Platform
{
    public required OciPlatform TargetPlatform { get; set; }
    public required string DockerfilePath { get; set; }
    public string[] Tags { get; set; } = [];
    public string[] UndocumentedTags { get; set; } = [];
}

public sealed record OciPlatform
{
    [YamlMember(Alias = "os")]
    public required OS OS { get; set; }
    public required Architecture Architecture { get; set; }
    public Variant? Variant { get; set; }

    public override string ToString() =>
        string.Join('/', new object?[] { OS, Architecture, Variant }.OfType<object>()).ToLowerInvariant();
}

public sealed record Readme
{
    public required string Path { get; set; }
    public string? TemplatePath { get; set; }
}

public enum OS
{
    Linux,
    Windows,
}

public enum Architecture
{
    Amd64,
    Arm,
    Arm64,
}

public enum Variant
{
    V7,
    V8,
}
