// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

public sealed record ManifestInfo(Manifest Model, ImmutableList<RepoInfo> Repos)
{
    private readonly ImmutableDictionary<string, RepoInfo> _reposById =
        Repos.Where(repo => repo.Model.Id is not null)
            .ToImmutableDictionary(repo => repo.Model.Id!);

    private readonly ImmutableDictionary<string, RepoInfo> _reposByName =
        Repos.ToImmutableDictionary(repo => repo.Model.Name);

    public RepoInfo? GetRepoById(string id) => _reposById.GetValueOrDefault(id);
    public RepoInfo? GetRepoByName(string name) => _reposByName.GetValueOrDefault(name);
}

public sealed record RepoInfo(Repo Model, Manifest Manifest, ImmutableList<ImageInfo> Images)
{
    public static RepoInfo Create(Repo model, Manifest manifest)
    {
        var imageInfos = model.Images
            .Select(image => ImageInfo.Create(image, model))
            .ToImmutableList();

        return new RepoInfo(model, manifest, imageInfos);
    }
}

public sealed record ImageInfo(Image Model, ImmutableList<PlatformInfo> Platforms)
{
    public static ImageInfo Create(Image model, Repo repo)
    {
        var platformInfos = model.Platforms
            .Select(platform => PlatformInfo.Create(platform, model))
            .ToImmutableList();

        return new ImageInfo(model, platformInfos);
    }
}

public sealed record PlatformInfo(Platform Model, Image Image)
{
    public static PlatformInfo Create(Platform model, Image image)
    {
        return new PlatformInfo(model, image);
    }
}
