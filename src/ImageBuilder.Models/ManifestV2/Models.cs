// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

public sealed record Manifest(
    Readme Readme,
    IDictionary<string, Repo> Repos,
    IDictionary<string, string> Variables);

public sealed record Repo(
    string Id,
    IDictionary<string, Image> Images,
    string McrTagsMetadataTemplate,
    string Name,
    Readme[] Readmes);

public sealed record Image(
    IDictionary<string, Platform> Platforms,
    Tag[] Tags,
    string ProductVersion,
    string Syndication);

public sealed record Platform(
    TargetPlatform TargetPlatform,
    IDictionary<string, string> BuildArgs,
    string Dockerfile,
    string? DockerfileTemplate,
    string OsVersion,
    Tag[] Tags);

public sealed record TargetPlatform(
    OS OS,
    Arch Arch);

public sealed record Tag(
    string Name,
    bool Undocumented);

public sealed record Readme(
    string Path,
    string? TemplatePath);

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
