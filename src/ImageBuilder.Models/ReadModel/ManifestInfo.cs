// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

public sealed record ManifestInfo(
    Manifest Model,
    string FilePath,
    ManifestReadmeInfo? Readme,
    ImmutableList<RepoInfo> Repos)
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

        var readmeInfo = model.Readme is not null
            ? ManifestReadmeInfo.Create(model.Readme, model, manifestDir)
            : null;

        return new ManifestInfo(model, manifestFilePath, readmeInfo, repoInfos);
    }
}

public sealed record ManifestReadmeInfo(Readme Model, Manifest Manifest, string FilePath, string? TemplatePath)
{
    internal static ManifestReadmeInfo Create(Readme model, Manifest manifest, string manifestDir)
    {
        string path = Path.Combine(manifestDir, model.Path);
        string? templatePath = PathHelper.MaybeCombine(manifestDir, model.TemplatePath);

        return new ManifestReadmeInfo(model, manifest, path, templatePath);
    }
}

public sealed record RepoReadmeInfo(Readme Model, Repo Repo, string FilePath, string? TemplatePath)
{
    internal static RepoReadmeInfo Create(Readme model, Repo repo, string manifestDir)
    {
        string path = Path.Combine(manifestDir, model.Path);
        string? templatePath = PathHelper.MaybeCombine(manifestDir, model.TemplatePath);

        return new RepoReadmeInfo(model, repo, path, templatePath);
    }
}

public sealed record RepoInfo(
    Repo Model,
    Manifest Manifest,
    string FullName,
    ImmutableList<ImageInfo> Images,
    ImmutableList<RepoReadmeInfo> Readmes)
{
    internal static RepoInfo Create(Repo model, Manifest manifest, string manifestDir)
    {
        var imageInfos = model.Images
            .Select(image => ImageInfo.Create(image, model, manifestDir))
            .ToImmutableList();

        var readmeInfos = model.Readmes
            .Select(readme => RepoReadmeInfo.Create(readme, model, manifestDir))
            .ToImmutableList();

        var fullName = manifest.Registry + "/" + model.Name;

        return new RepoInfo(model, manifest, fullName, imageInfos, readmeInfos);
    }
}

public sealed record ImageInfo(Image Model, Repo repo, ImmutableList<PlatformInfo> Platforms)
{
    internal static ImageInfo Create(Image model, Repo repo, string manifestDir)
    {
        var platformInfos = model.Platforms
            .Select(platform => PlatformInfo.Create(platform, model, manifestDir))
            .ToImmutableList();

        return new ImageInfo(model, repo, platformInfos);
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
