// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Resolves the current canonical identity of a platform's base image.
/// </summary>
public sealed class BaseImageResolver
{
    private readonly ImageDigestCache _imageDigestCache;
    private readonly ImageNameResolver _imageNameResolver;
    private readonly Dictionary<string, string> _plannedImageDigests = [];
    private readonly bool _isDryRun;
    private readonly bool _useLocalImage;

    private BaseImageResolver(
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        bool useLocalImage,
        bool isDryRun)
    {
        _imageDigestCache = imageDigestCache ?? throw new ArgumentNullException(nameof(imageDigestCache));
        _imageNameResolver = imageNameResolver ?? throw new ArgumentNullException(nameof(imageNameResolver));
        _useLocalImage = useLocalImage;
        _isDryRun = isDryRun;
    }

    /// <summary>
    /// Creates a resolver for base images available to the current build.
    /// </summary>
    /// <remarks>
    /// For uncached tags, resolution verifies that the registry's current digest is associated
    /// with the image in the local Docker store. Digests recorded by the current build are trusted.
    /// </remarks>
    public static BaseImageResolver CreateForLocalImages(
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        bool isDryRun) =>
        new(imageDigestCache, imageNameResolver, useLocalImage: true, isDryRun);

    /// <summary>
    /// Creates a resolver for base images expected to exist in a remote registry.
    /// </summary>
    public static BaseImageResolver CreateForRegistryImages(
        ImageDigestCache imageDigestCache,
        ImageNameResolver imageNameResolver,
        bool isDryRun) =>
        new(imageDigestCache, imageNameResolver, useLocalImage: false, isDryRun);

    /// <summary>
    /// Records an image that a provisional plan expects to make available under the given tag.
    /// </summary>
    /// <remarks>
    /// This allows dependent platforms to be planned before a cached parent image is pulled and
    /// retagged in the local Docker store.
    /// </remarks>
    internal void RecordPlannedAvailableImage(string tag, string digest) =>
        _plannedImageDigests[tag] = digest;

    /// <summary>
    /// Resolves the digest SHA of the platform's base image, or returns <see langword="null"/>
    /// when the platform has no base image or the image does not yet exist.
    /// </summary>
    public async Task<string?> ResolveDigestShaAsync(PlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        if (platform.FinalStageFromImage is not string fromImage)
        {
            return null;
        }

        if (!_useLocalImage)
        {
            try
            {
                return await _imageDigestCache.GetManifestDigestShaAsync(
                    _imageNameResolver.GetFinalStageImageNameForDigestQuery(platform),
                    _isDryRun);
            }
            // An image may not have been published yet. Authentication and other failures must propagate.
            catch (Exception ex) when (IsImageNotFoundException(ex))
            {
                return null;
            }
        }

        string localTag = _imageNameResolver.GetFromImageLocalTag(fromImage);
        string? localDigest = _plannedImageDigests.TryGetValue(localTag, out string? plannedDigest) ?
            plannedDigest :
            await _imageDigestCache.GetLocalImageDigestAsync(localTag, _isDryRun);

        return localDigest is null ? null : DockerHelper.GetDigestSha(localDigest);
    }

    private static bool IsImageNotFoundException(Exception ex) =>
        ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound } or
        RequestFailedException { Status: 404 };
}
