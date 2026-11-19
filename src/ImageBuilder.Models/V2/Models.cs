// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

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
    public required Dictionary<string, ImagePlatform> Platforms { get; set; }
}

public sealed record ImagePlatform
{
    public required string Platform { get; set; }
    public required string DockerfilePath { get; set; }
    public string[] Tags { get; set; } = [];
    public string[] UndocumentedTags { get; set; } = [];
}

public sealed record Readme
{
    public required string Path { get; set; }
    public string? TemplatePath { get; set; }
}
