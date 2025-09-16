// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

public sealed record ManifestInfo(Manifest Model, ImmutableList<RepoInfo> Repos)
{
    public static ManifestInfo Create(Manifest model)
    {
        var repoInfos = model.Repos.Select(RepoInfo.Create).ToImmutableList();
        return new ManifestInfo(model, repoInfos);
    }
}

public sealed record RepoInfo(Repo Model, ImmutableList<ImageInfo> Images)
{
    public static RepoInfo Create(Repo model)
    {
        var imageInfos = model.Images.Select(ImageInfo.Create).ToImmutableList();
        return new RepoInfo(model, imageInfos);
    }
}

public sealed record ImageInfo(Image Model, ImmutableList<PlatformInfo> Platforms)
{
    public static ImageInfo Create(Image model)
    {
        var platformInfos = model.Platforms.Select(PlatformInfo.Create).ToImmutableList();
        return new ImageInfo(model, platformInfos);
    }
}

public sealed record PlatformInfo(Platform Model)
{
    public static PlatformInfo Create(Platform model)
    {
        return new PlatformInfo(model);
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
