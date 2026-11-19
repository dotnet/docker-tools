// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

/// <summary>
/// Holds information about what was produced by an image build.
/// </summary>
public sealed record ImageArtifactDetails
{
    /// <summary>
    /// OCI image manifests (or Docker V2 manifests) produced by a build.
    /// The key of the dictionary is constructed by combining the repo, image,
    /// and platform keys defined in the <see cref="Manifest"/> model.
    /// </summary>
    public Dictionary<string, ImageManifest> Manifests { get; set; } = [];

    /// <summary>
    /// OCI image indexes (or Docker V2 manifest lists) produced by a build.
    /// The key of the dictionary is constructed by combining the repo and
    /// image keys defined in the <see cref="Manifest"/> model.
    /// </summary>
    public Dictionary<string, ImageIndex> Indexes { get; set; } = [];
}

/// <summary>
/// OCI image manifest or Docker V2 manifest produced by a build.
/// </summary>
public sealed record ImageManifest
{
    /// <summary>
    /// OCI registry where the image manifest is stored.
    /// </summary>
    public required string Registry { get; set; }

    /// <summary>
    /// OCI repository where the image manifest is stored.
    /// </summary>
    public required string Repository { get; set; }

    /// <summary>
    /// The digest of the image manifest, in the format "algorithm:hash".
    /// </summary>
    public required string Digest { get; set; }

    /// <summary>
    /// The tags that point to this image manifest.
    /// </summary>
    public string[] Tags { get; set; } = [];

    /// <summary>
    /// The base image used to build this image manifest.
    /// </summary>
    public BaseImage? BaseImage { get; set; }
}

/// <summary>
/// OCI image index or Docker V2 manifest list produced by a build.
/// </summary>
public sealed record ImageIndex
{
    /// <summary>
    /// OCI registry where the image index is stored.
    /// </summary>
    public required string Registry { get; set; }

    /// <summary>
    /// OCI repository where the image index is stored.
    /// </summary>
    public required string Repository { get; set; }

    /// <summary>
    /// The digest of the image index, in the format "algorithm:hash".
    /// </summary>
    public required string Digest { get; set; }

    /// <summary>
    /// The tags that point to this image index.
    /// </summary>
    public string[] Tags { get; set; } = [];

    /// <summary>
    /// The digests of the manifests which constitute this image index, in the
    /// format "algorithm:hash".
    /// </summary>
    /// <remarks>
    /// Since image indexes cannot span multiple repositories, the digest alone
    /// is sufficient to unambiguously identify each manifest.
    /// </remarks>
    public required string[] Manifests { get; set; }
}

public sealed record BaseImage
{
    /// <summary>
    /// The fully qualified name of the base image, including the registry, repository, and tag.
    /// </summary>
    public required string Image { get; set; }

    /// <summary>
    /// The digest of the base image, in the format "algorithm:hash".
    /// </summary>
    public required string Digest { get; set; }
}
