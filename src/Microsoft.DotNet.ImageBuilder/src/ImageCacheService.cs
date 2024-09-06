// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IImageCacheService
{
    bool HasAnyCachedPlatforms { get; }

    Task<ImageCacheResult> CheckForCachedImageAsync(
        ImageData? srcImageData,
        PlatformData platformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        string? sourceRepoUrl,
        bool isDryRun);
}

[Export(typeof(IImageCacheService))]
public class ImageCacheService : IImageCacheService
{
    private readonly ILoggerService _loggerService;
    private readonly IGitService _gitService;

    // Metadata about Dockerfiles whose images have been retrieved from the cache
    private readonly Dictionary<string, PlatformData> _cachedPlatforms = [];

    [ImportingConstructor]
    public ImageCacheService(ILoggerService loggerService, IGitService gitService)
    {
        _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
    }

    public bool HasAnyCachedPlatforms => _cachedPlatforms.Any();

    public async Task<ImageCacheResult> CheckForCachedImageAsync(
        ImageData? srcImageData,
        PlatformData platformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        string? sourceRepoUrl,
        bool isDryRun)
    {
        ImageCacheState cacheState = ImageCacheState.NotCached;
        bool isNewCacheHit = false;
        PlatformData? srcPlatformData = srcImageData?.Platforms
            .FirstOrDefault(srcPlatform => srcPlatform.PlatformInfo == platformData.PlatformInfo);

        if (platformData.PlatformInfo is null)
        {
            throw new Exception("Expected platform info to be set");
        }

        string cacheKey = GetBuildCacheKey(platformData.PlatformInfo);
        if (_cachedPlatforms.TryGetValue(cacheKey, out PlatformData? cachedPlatform))
        {
            cacheState = ImageCacheState.Cached;
            if (srcPlatformData is null ||
                !CachedPlatformHasAllTagsPublished(srcPlatformData))
            {
                cacheState = ImageCacheState.CachedWithMissingTags;
            }
            return new ImageCacheResult(cacheState, isNewCacheHit, cachedPlatform);
        }

        // If this Dockerfile has been built and published before
        if (srcPlatformData != null)
        {
            bool isCachedImage = await CheckForCachedImageFromImageInfoAsync(
                platformData.PlatformInfo,
                srcPlatformData,
                imageDigestCache,
                imageNameResolver,
                sourceRepoUrl,
                isDryRun);

            if (isCachedImage)
            {
                isNewCacheHit = true;
                cacheState = ImageCacheState.Cached;
                if (!CachedPlatformHasAllTagsPublished(srcPlatformData))
                {
                    cacheState = ImageCacheState.CachedWithMissingTags;
                }
                _cachedPlatforms[cacheKey] = srcPlatformData;
            }
        }

        return new ImageCacheResult(cacheState, isNewCacheHit, srcPlatformData);
    }

    private static bool CachedPlatformHasAllTagsPublished(PlatformData srcPlatformData) =>
        (srcPlatformData.PlatformInfo?.Tags ?? [])
            .Where(tag => !tag.Model.IsLocal)
            .Select(tag => tag.Name)
            .AreEquivalent(srcPlatformData.SimpleTags);

    private async Task<bool> CheckForCachedImageFromImageInfoAsync(
        PlatformInfo platform,
        PlatformData srcPlatformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        string? sourceRepoUrl,
        bool isDryRun)
    {
        _loggerService.WriteMessage($"Checking for cached image for '{platform.DockerfilePathRelativeToManifest}'");

        // If the previously published image was based on an image that is still the latest version AND
        // the Dockerfile hasn't changed since it was last published
        if (await IsBaseImageDigestUpToDateAsync(
                platform, srcPlatformData, imageDigestCache, imageNameResolver, isDryRun) &&
            IsDockerfileUpToDate(platform, srcPlatformData, sourceRepoUrl))
        {
            return true;
        }

        _loggerService.WriteMessage("CACHE MISS");
        _loggerService.WriteMessage();

        return false;
    }

    private async Task<bool> IsBaseImageDigestUpToDateAsync(
        PlatformInfo platform,
        PlatformData srcPlatformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        bool isDryRun)
    {
        _loggerService.WriteMessage();

        if (platform.FinalStageFromImage is null)
        {
            _loggerService.WriteMessage($"Image does not have a base image. By default, it is considered up-to-date.");
            return true;
        }

        string? currentBaseImageDigest = await imageDigestCache.GetLocalImageDigestAsync(
            imageNameResolver.GetFromImageLocalTag(platform.FinalStageFromImage),
            isDryRun);

        string? imageInfoSha = srcPlatformData.BaseImageDigest is not null ?
            DockerHelper.GetDigestSha(srcPlatformData.BaseImageDigest) :
            null;
        
        bool baseImageDigestMatches = imageInfoSha?.Equals(currentSha, StringComparison.OrdinalIgnoreCase) == true;

        _loggerService.WriteMessage($"Image info's base image digest SHA: {imageInfoSha}");
        _loggerService.WriteMessage($"Latest base image digest SHA: {currentSha}");
        _loggerService.WriteMessage($"Base image digests match: {baseImageDigestMatches}");
        return baseImageDigestMatches;
    }

    private bool IsDockerfileUpToDate(PlatformInfo platform, PlatformData srcPlatformData, string? sourceRepoUrl)
    {
        string currentCommitUrl = _gitService.GetDockerfileCommitUrl(platform, sourceRepoUrl);
        bool commitShaMatches = false;
        if (srcPlatformData.CommitUrl is not null)
        {
            commitShaMatches = srcPlatformData.CommitUrl.Equals(currentCommitUrl, StringComparison.OrdinalIgnoreCase);
        }

        _loggerService.WriteMessage();
        _loggerService.WriteMessage($"Image info's Dockerfile commit: {srcPlatformData.CommitUrl}");
        _loggerService.WriteMessage($"Latest Dockerfile commit: {currentCommitUrl}");
        _loggerService.WriteMessage($"Dockerfile commits match: {commitShaMatches}");
        return commitShaMatches;
    }

    private static string GetBuildCacheKey(PlatformInfo platform) =>
        $"{platform.DockerfilePathRelativeToManifest}-" +
        string.Join('-', platform.BuildArgs.Select(kvp => $"{kvp.Key}={kvp.Value}").ToArray());
}

public enum ImageCacheState
{
    /// <summary>
    /// Indicates a previously built image was not found in the registry.
    /// </summary>
    NotCached,

    /// <summary>
    /// Indicates a previously built image was found in the registry.
    /// </summary>
    Cached,

    /// <summary>
    /// Indicates a previously built image was found in the registry but is missing new tags.
    /// </summary>
    CachedWithMissingTags
}

public record ImageCacheResult(ImageCacheState State, bool IsNewCacheHit, PlatformData? Platform);
