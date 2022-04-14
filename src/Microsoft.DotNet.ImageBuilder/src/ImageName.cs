// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public class ImageName
{
    public ImageName(string? registry, string repo, string? tag, string? digest)
    {
        Registry = registry;
        Repo = repo;
        Tag = tag;
        Digest = digest;
    }

    public string? Registry { get; }
    public string Repo { get; }
    public string? Tag { get; }
    public string? Digest { get; }

    public static ImageName Parse(string imageName, bool autoResolveRepoName = false)
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

        string? tag = null;
        string? digest = null;

        separatorIndex = imageName.IndexOf('@');
        if (separatorIndex < 0)
        {
            separatorIndex = imageName.IndexOf(':');
            if (separatorIndex >= 0)
            {
                tag = imageName[(separatorIndex + 1)..];
            }
        }
        else
        {
            digest = imageName[(separatorIndex + 1)..];
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

        if (autoResolveRepoName && registry is null && !repo.Contains('/'))
        {
            repo = $"library/{repo}";
        }

        return new ImageName(registry, repo, tag, digest);
    }
}
#nullable disable
