// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json;
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

    public static ManifestInfo Create(Manifest model)
    {
        var repoInfos = model.Repos
            .Select(repo => RepoInfo.Create(repo, model))
            .ToImmutableList();

        return new ManifestInfo(model, repoInfos);
    }
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

internal static class ManifestJsonHelper
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}

public static class ManifestInfoExtensions
{
    extension(ManifestInfo manifestInfo)
    {
        public static async Task<ManifestInfo> LoadAsync(string manifestPath)
        {
            Manifest manifest = await Manifest.LoadFromFileAsync(manifestPath);
            return ManifestInfo.Create(manifest);
        }

        public static ManifestInfo Deserialize(string manifestPath)
        {
            Manifest manifest = Manifest.Deserialize(manifestPath);
            return ManifestInfo.Create(manifest);
        }
    }
}

public static class ManifestReadModelExtensions
{
    extension(Manifest manifest)
    {
        public static async Task<Manifest> LoadFromFileAsync(string manifestPath)
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            var manifestObject = Manifest.Deserialize(json);
            return manifestObject;
        }

        public static Manifest Deserialize(string json)
        {
            return JsonSerializer.Deserialize<Manifest>(json, ManifestJsonHelper.JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize manifest from content: '{json}'");
        }
    }
}
