// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Service for determining whether a Docker image can be retrieved from a cache
/// rather than being rebuilt.
/// </summary>
public interface IImageCacheService
{
    /// <summary>
    /// Gets whether any platforms have been identified as cached during this session.
    /// </summary>
    bool HasAnyCachedPlatforms { get; }

    /// <summary>
    /// Checks whether a previously built image can be reused from cache.
    /// </summary>
    /// <param name="srcImageData">Image data from the source image-info file, if available.</param>
    /// <param name="platformData">Platform data for the image being checked.</param>
    /// <param name="imageDigestCache">Cache for looking up image digests from registries.</param>
    /// <param name="imageNameResolver">Resolver for constructing image names for digest queries.</param>
    /// <param name="sourceRepoUrl">URL of the source repository containing the Dockerfiles.</param>
    /// <param name="isLocalBaseImageExpected">Whether the base image is expected to exist locally rather than in a remote registry.</param>
    /// <param name="isDryRun">Whether this is a dry run that should skip actual registry calls.</param>
    Task<ImageCacheResult> CheckForCachedImageAsync(
        ImageData? srcImageData,
        PlatformData platformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        string? sourceRepoUrl,
        bool isLocalBaseImageExpected,
        bool isDryRun);
}

/// <inheritdoc/>
public class ImageCacheService : IImageCacheService
{
    private readonly ILogger<ImageCacheService> _logger;
    private readonly IGitService _gitService;

    private readonly object _cachedPlatformsLock = new();

    /// <summary>
    /// Metadata about Dockerfiles whose images have been retrieved from the cache.
    /// Keyed by the build cache key derived from the platform's Dockerfile path and build args.
    /// </summary>
    private readonly Dictionary<string, PlatformData> _cachedPlatforms = [];

    public ImageCacheService(ILogger<ImageCacheService> logger, IGitService gitService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <summary>
    /// Checks whether the source platform data has all its expected tags published.
    /// </summary>
    private static bool CachedPlatformHasAllTagsPublished(PlatformData srcPlatformData) =>
        (srcPlatformData.PlatformInfo?.Tags ?? [])
            .Select(tag => tag.Name)
            .AreEquivalent(srcPlatformData.SimpleTags);

    /// <summary>
    /// Determines whether a previously published image can be reused by comparing the base image
    /// digest and Dockerfile commit against the current state.
    /// </summary>
    private async Task<bool> CheckForCachedImageFromImageInfoAsync(
        PlatformInfo platform,
        PlatformData srcPlatformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        string? sourceRepoUrl,
        bool isLocalBaseImageExpected,
        bool isDryRun)
    {
        _logger.LogInformation("Checking for cached image for '{DockerfilePath}'", platform.DockerfilePathRelativeToManifest);

        // If the previously published image was based on an image that is still the latest version AND
        // the Dockerfile hasn't changed since it was last published
        if (await IsBaseImageDigestUpToDateAsync(
                platform, srcPlatformData, imageDigestCache, imageNameResolver, isLocalBaseImageExpected, isDryRun) &&
            IsDockerfileUpToDate(platform, srcPlatformData, sourceRepoUrl))
        {
            return true;
        }

        _logger.LogInformation("CACHE MISS");
        _logger.LogInformation(string.Empty);

        return false;
    }

    /// <summary>
    /// Checks whether the base image digest recorded in image-info matches the current digest
    /// available from the registry.
    /// </summary>
    private async Task<bool> IsBaseImageDigestUpToDateAsync(
        PlatformInfo platform,
        PlatformData srcPlatformData,
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        bool isLocalImageExpected,
        bool isDryRun)
    {
        _logger.LogInformation(string.Empty);

        if (platform.FinalStageFromImage is null)
        {
            _logger.LogInformation("Image does not have a base image. By default, it is considered up-to-date.");
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
            // Handle cases where the image is not found in the registry yet.
            // Other errors (e.g., authentication failures) should propagate so
            // they are not silently swallowed. See https://github.com/dotnet/docker-tools/issues/1964
            catch (Exception ex) when (IsImageNotFoundException(ex))
            {
                currentSha = null;
            }
        }

        string? imageInfoSha = srcPlatformData.BaseImageDigest is not null ?
            DockerHelper.GetDigestSha(srcPlatformData.BaseImageDigest) :
            null;

        bool baseImageDigestMatches = imageInfoSha?.Equals(currentSha, StringComparison.OrdinalIgnoreCase) == true;

        _logger.LogInformation("Image info's base image digest SHA: {ImageInfoSha}", imageInfoSha);
        _logger.LogInformation("Latest base image digest SHA: {CurrentSha}", currentSha);
        _logger.LogInformation("Base image digests match: {BaseImageDigestMatches}", baseImageDigestMatches);
        return baseImageDigestMatches;
    }

    /// <summary>
    /// Checks whether the Dockerfile has changed since the last published build by comparing
    /// the current git commit URL against the one recorded in image-info.
    /// </summary>
    private bool IsDockerfileUpToDate(PlatformInfo platform, PlatformData srcPlatformData, string? sourceRepoUrl)
    {
        string currentCommitUrl = _gitService.GetDockerfileCommitUrl(platform, sourceRepoUrl);
        bool commitShaMatches = false;
        if (srcPlatformData.CommitUrl is not null)
        {
            commitShaMatches = srcPlatformData.CommitUrl.Equals(currentCommitUrl, StringComparison.OrdinalIgnoreCase);
        }

        _logger.LogInformation(string.Empty);
        _logger.LogInformation("Image info's Dockerfile commit: {CommitUrl}", srcPlatformData.CommitUrl);
        _logger.LogInformation("Latest Dockerfile commit: {CurrentCommitUrl}", currentCommitUrl);
        _logger.LogInformation("Dockerfile commits match: {CommitShaMatches}", commitShaMatches);
        return commitShaMatches;
    }

    /// <summary>
    /// Builds a cache key that uniquely identifies a platform build based on its Dockerfile path
    /// and build arguments.
    /// </summary>
    private static string GetBuildCacheKey(PlatformInfo platform) =>
        $"{platform.DockerfilePathRelativeToManifest}-" +
        string.Join('-', platform.BuildArgs.Select(kvp => $"{kvp.Key}={kvp.Value}").ToArray());

    /// <summary>
    /// Returns true if the exception represents an HTTP 404 Not Found response,
    /// indicating the image does not exist in the registry.
    /// </summary>
    private static bool IsImageNotFoundException(Exception ex) =>
        (ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound }) ||
        (ex is RequestFailedException { Status: 404 });
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

/// <summary>
/// The result of checking whether an image is cached.
/// </summary>
/// <param name="State">The cache state of the image.</param>
/// <param name="IsNewCacheHit">Whether this is a newly discovered cache hit (not previously known in this session).</param>
/// <param name="Platform">The source platform data associated with the cached image, if available.</param>
public record ImageCacheResult(ImageCacheState State, bool IsNewCacheHit, PlatformData? Platform);
