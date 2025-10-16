// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
        bool isLocalBaseImageExpected,
        bool isDryRun);
}
public class ImageCacheService : IImageCacheService
{
    private readonly ILoggerService _loggerService;
    private readonly IGitService _gitService;

    private readonly object _cachedPlatformsLock = new();

    // Metadata about Dockerfiles whose images have been retrieved from the cache
    private readonly Dictionary<string, PlatformData> _cachedPlatforms = [];

    public ImageCacheService(ILoggerService loggerService, IGitService gitService)
    {
        _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
    }

    public bool HasAnyCachedPlatforms
    {
        get
        {
            lock (_cachedPlatformsLock)
            {
                return _cachedPlatforms.Any();
            }
        }
    }

    public async Task<ImageCacheResult> CheckForCachedImageAsync(
        ImageData? srcImageData,
        PlatformData platformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        string? sourceRepoUrl,
        bool isLocalBaseImageExpected,
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
        lock (_cachedPlatformsLock)
        {
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
                isLocalBaseImageExpected,
                isDryRun);

            if (isCachedImage)
            {
                isNewCacheHit = true;
                cacheState = ImageCacheState.Cached;
                if (!CachedPlatformHasAllTagsPublished(srcPlatformData))
                {
                    cacheState = ImageCacheState.CachedWithMissingTags;
                }
                lock (_cachedPlatformsLock)
                {
                    _cachedPlatforms[cacheKey] = srcPlatformData;
                }
            }
        }

        return new ImageCacheResult(cacheState, isNewCacheHit, srcPlatformData);
    }

    private static bool CachedPlatformHasAllTagsPublished(PlatformData srcPlatformData) =>
        (srcPlatformData.PlatformInfo?.Tags ?? [])
            .Select(tag => tag.Name)
            .AreEquivalent(srcPlatformData.SimpleTags);

    private async Task<bool> CheckForCachedImageFromImageInfoAsync(
        PlatformInfo platform,
        PlatformData srcPlatformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        string? sourceRepoUrl,
        bool isLocalBaseImageExpected,
        bool isDryRun)
    {
        _loggerService.WriteMessage($"Checking for cached image for '{platform.DockerfilePathRelativeToManifest}'");

        // If the previously published image was based on images that are still the latest versions AND
        // the Dockerfile hasn't changed since it was last published
        if (await AreFromImageDigestsUpToDateAsync(
                platform, srcPlatformData, imageDigestCache, imageNameResolver, isLocalBaseImageExpected, isDryRun) &&
            IsDockerfileUpToDate(platform, srcPlatformData, sourceRepoUrl))
        {
            return true;
        }

        _loggerService.WriteMessage("CACHE MISS");
        _loggerService.WriteMessage();

        return false;
    }

    private async Task<bool> AreFromImageDigestsUpToDateAsync(
        PlatformInfo platform,
        PlatformData srcPlatformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        bool isLocalImageExpected,
        bool isDryRun)
    {
        _loggerService.WriteMessage();

        // If there are no FROM images recorded in the platform data, fall back to the legacy behavior
        // of checking only the final stage base image
        if (srcPlatformData.FromImages is null || srcPlatformData.FromImages.Count == 0)
        {
            return await IsBaseImageDigestUpToDateAsync(
                platform, srcPlatformData, imageDigestCache, imageNameResolver, isLocalImageExpected, isDryRun);
        }

        // Check all FROM images (both intermediate and final stages)
        IEnumerable<string> allFromImages = platform.InternalFromImages
            .Concat(platform.ExternalFromImages)
            .Distinct();

        foreach (string fromImage in allFromImages)
        {
            string? currentSha = null;

            if (isLocalImageExpected)
            {
                string localTag = imageNameResolver.GetFromImageLocalTag(fromImage);
                currentSha = await imageDigestCache.GetLocalImageDigestAsync(localTag, isDryRun);
                if (currentSha is not null)
                {
                    currentSha = DockerHelper.GetDigestSha(currentSha);
                }
            }
            else
            {
                try
                {
                    string queryImage = imageNameResolver.GetFromImagePullTag(fromImage);
                    currentSha = await imageDigestCache.GetManifestDigestShaAsync(queryImage, isDryRun);
                }
                // Handle cases where the image is not found in the registry yet
                catch (Exception)
                {
                    currentSha = null;
                }
            }

            // Get the recorded digest for this FROM image
            string? recordedDigest = null;
            if (srcPlatformData.FromImages.TryGetValue(fromImage, out string? digest))
            {
                recordedDigest = DockerHelper.GetDigestSha(digest);
            }

            bool digestMatches = recordedDigest?.Equals(currentSha, StringComparison.OrdinalIgnoreCase) == true;

            _loggerService.WriteMessage($"FROM image: {fromImage}");
            _loggerService.WriteMessage($"  Image info's digest SHA: {recordedDigest}");
            _loggerService.WriteMessage($"  Latest digest SHA: {currentSha}");
            _loggerService.WriteMessage($"  Digests match: {digestMatches}");

            if (!digestMatches)
            {
                _loggerService.WriteMessage($"FROM image '{fromImage}' has changed. Cache miss.");
                return false;
            }
        }

        _loggerService.WriteMessage("All FROM images are up-to-date.");
        return true;
    }

    private async Task<bool> IsBaseImageDigestUpToDateAsync(
        PlatformInfo platform,
        PlatformData srcPlatformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        bool isLocalImageExpected,
        bool isDryRun)
    {
        if (platform.FinalStageFromImage is null)
        {
            _loggerService.WriteMessage($"Image does not have a base image. By default, it is considered up-to-date.");
            return true;
        }

        string queryImage = imageNameResolver.GetFinalStageImageNameForDigestQuery(platform);

        string? currentSha;
        if (isLocalImageExpected)
        {
            currentSha = await imageDigestCache.GetLocalImageDigestAsync(
                imageNameResolver.GetFromImageLocalTag(platform.FinalStageFromImage), isDryRun);
            if (currentSha is not null)
            {
                currentSha = DockerHelper.GetDigestSha(currentSha);
            }
        }
        else
        {
            try
            {
                currentSha = await imageDigestCache.GetManifestDigestShaAsync(queryImage, isDryRun);
            }
            // Handle cases where the image is not found in the registry yet
            catch (Exception)
            {
                currentSha = null;
            }
        }

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

[Flags]
public enum ImageCacheState
{
    /// <summary>
    /// Indicates a previously built image was not found in the registry.
    /// </summary>
    NotCached = 0,

    /// <summary>
    /// Indicates a previously built image was found in the registry.
    /// </summary>
    Cached = 1,

    /// <summary>
    /// Indicates a previously built image was found in the registry but is missing new tags.
    /// </summary>
    CachedWithMissingTags = Cached | 2
}

public record ImageCacheResult(ImageCacheState State, bool IsNewCacheHit, PlatformData? Platform);
