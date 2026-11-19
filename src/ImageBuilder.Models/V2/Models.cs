// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public sealed record Manifest
{
    /// <summary>
    /// The registry where all repositories are located.
    /// </summary>
    public string? Registry { get; set; }

    /// <summary>
    /// Repositories that belong in this manifest.
    /// The dictionary key should be a stable identifier for repos between builds.
    /// </summary>
    public required Dictionary<string, Repo> Repos { get; set; }
}

public sealed record Repo
{
    /// <summary>
    /// The name of the repository as it will appear in the registry.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Readme files that will be published to MCR.
    /// </summary>
    public Readme[] Readmes { get; set; } = [];

    /// <summary>
    /// Images that belong in this repository.
    /// The dictionary key should be a stable identifier for images between builds.
    /// </summary>
    public required Dictionary<string, Image> Images { get; set; }
}

public sealed record Image
{
    /// <summary>
    /// Tags that are shared across all platforms of this image.
    /// </summary>
    public string[] SharedTags { get; set; } = [];

    /// <summary>
    /// Platforms that constitute this image.
    /// The dictionary key should be a stable identifier for platforms between builds.
    /// </summary>
    public required Dictionary<string, ImagePlatform> Platforms { get; set; }
}

public sealed record ImagePlatform
{
    /// <summary>
    /// The OCI platform to build the image for. Examples: linux/amd64, linux/arm64, windows/amd64.
    /// It is intended to be passed verbatim to the build tool (Docker, Podman, etc.)
    /// </summary>
    public required string Platform { get; set; }

    /// <summary>
    /// The path to the Dockerfile for this platform, relative to the manifest file.
    /// </summary>
    public required string DockerfilePath { get; set; }

    /// <summary>
    /// Tags that are specific to this platform of the image.
    /// </summary>
    public string[] Tags { get; set; } = [];
}

public sealed record Readme
{
    /// <summary>
    /// The path to the readme file, relative to the manifest file.
    /// </summary>
    public required string FilePath { get; set; }
}
