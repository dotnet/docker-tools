// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public record ImageName
{
    public ImageName(string? registry, string repo, string? tag, string? digest)
    {
        Registry = registry ?? "";
        Repo = repo;
        Tag = tag ?? "";
        Digest = digest ?? "";
    }

    public string Registry { get; }
    public string Repo { get; }
    public string Tag { get; }
    public string Digest { get; }

    /// <summary>
    /// Implicitly converts a string to an ImageName.
    /// </summary>
    public static implicit operator ImageName(string imageName) => Parse(imageName, true);

    /// <summary>
    /// Implicitly converts an ImageName to a string.
    /// </summary>
    public static implicit operator string(ImageName imageName) => imageName.ToString();

    /// <summary>
    /// Formats the image name as a string.
    /// </summary>
    public override string ToString()
    {
        string imageName = string.IsNullOrWhiteSpace(Registry) ? Repo : $"{Registry}/{Repo}";

        if (!string.IsNullOrWhiteSpace(Tag))
        {
            imageName = $"{imageName}:{Tag}";
        }

        if (!string.IsNullOrWhiteSpace(Digest))
        {
            imageName = $"{imageName}@{Digest}";
        }

        return imageName;
    }

    /// <summary>
    /// Parses an image name into its constituent parts.
    /// </summary>
    /// <param name="imageName">The image name to parse.</param>
    /// <param name="autoResolveImpliedNames">Whether to resolve implied parts of the image name like repo and registry (for Docker Hub images).</param>
    public static ImageName Parse(string imageName, bool autoResolveImpliedNames = false)
    {
        string? registry = null;
        int separatorIndex = imageName.IndexOf('/');
        if (separatorIndex >= 0)
        {
            string firstSegment = imageName[..separatorIndex];
            if (firstSegment.Contains('.') || firstSegment.Contains(':'))
            {
                registry = firstSegment;
                imageName = imageName[(separatorIndex + 1)..];
            }
        }

        if (registry is null && autoResolveImpliedNames)
        {
            registry = DockerHelper.DockerHubRegistry;
        }

        string? tag = null;
        string? digest = null;

        separatorIndex = imageName.IndexOf('@');
        if (separatorIndex >= 0)
        {
            digest = imageName[(separatorIndex + 1)..];
        }
        else
        {
            separatorIndex = imageName.IndexOf(':');
            if (separatorIndex >= 0)
            {
                tag = imageName[(separatorIndex + 1)..];
            }
        }

        string repo;
        if (separatorIndex >= 0)
        {
            repo = imageName[..separatorIndex];
        }
        else
        {
            repo = imageName;
        }

        if (autoResolveImpliedNames && registry == DockerHelper.DockerHubRegistry && !repo.Contains('/'))
        {
            repo = $"library/{repo}";
        }

        return new ImageName(registry, repo, tag, digest);
    }
}
