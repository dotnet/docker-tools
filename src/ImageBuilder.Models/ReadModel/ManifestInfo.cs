// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

public sealed record ManifestInfo(Manifest Model, string FilePath, ImmutableList<RepoInfo> Repos)
{
    private readonly ImmutableDictionary<string, RepoInfo> _reposById =
        Repos.Where(repo => repo.Model.Id is not null)
            .ToImmutableDictionary(repo => repo.Model.Id!);

    private readonly ImmutableDictionary<string, RepoInfo> _reposByName =
        Repos.ToImmutableDictionary(repo => repo.Model.Name);

    public RepoInfo? GetRepoById(string id) => _reposById.GetValueOrDefault(id);
    public RepoInfo? GetRepoByName(string name) => _reposByName.GetValueOrDefault(name);

    internal static ManifestInfo Create(Manifest model, string manifestFilePath)
    {
        var manifestDir = Path.GetDirectoryName(manifestFilePath) ?? "";
        var repoInfos = model.Repos
            .Select(repo => RepoInfo.Create(repo, model, manifestDir))
            .ToImmutableList();

        return new ManifestInfo(model, manifestFilePath, repoInfos);
    }
}

public sealed record RepoInfo(Repo Model, Manifest Manifest, ImmutableList<ImageInfo> Images)
{
    internal static RepoInfo Create(Repo model, Manifest manifest, string manifestDir)
    {
        var imageInfos = model.Images
            .Select(image => ImageInfo.Create(image, model, manifestDir))
            .ToImmutableList();

        return new RepoInfo(model, manifest, imageInfos);
    }
}

public sealed record ImageInfo(Image Model, ImmutableList<PlatformInfo> Platforms)
{
    internal static ImageInfo Create(Image model, Repo repo, string manifestDir)
    {
        var platformInfos = model.Platforms
            .Select(platform => PlatformInfo.Create(platform, model, manifestDir))
            .ToImmutableList();

        return new ImageInfo(model, platformInfos);
    }
}

public sealed record PlatformInfo(
    Platform Model,
    Image Image,
    string DockerfilePath,
    string? DockerfileTemplatePath = null)
{
    internal static PlatformInfo Create(Platform model, Image image, string manifestDir)
    {
        var dockerfilePath = Path.Combine(manifestDir, model.Dockerfile, "Dockerfile");
        var dockerfileTemplatePath = model.DockerfileTemplate is not null
            ? Path.Combine(manifestDir, model.DockerfileTemplate) : null;

        return new PlatformInfo(model, image, dockerfilePath, dockerfileTemplatePath);
    }
}
