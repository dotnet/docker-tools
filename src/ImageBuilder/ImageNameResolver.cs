// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Controls how internal image names are resolved when querying for digests.
/// </summary>
public enum DigestResolutionMode
{
    /// <summary>
    /// Use the staging ACR registry as-is for internal image digest queries (build scenario).
    /// </summary>
    Staging,

    /// <summary>
    /// Use the public MCR registry for internal image digest queries (matrix generation scenario).
    /// </summary>
    Public
}

/// <inheritdoc/>
public class ImageNameResolver : IImageNameResolver
{
    private readonly BaseImageOverrideOptions _baseImageOverrideOptions;
    private readonly string? _repoPrefix;
    private readonly string? _sourceRepoPrefix;
    private readonly DigestResolutionMode _digestResolutionMode;
    private readonly ManifestInfo _manifest;

    public ImageNameResolver(
        DigestResolutionMode digestResolutionMode,
        BaseImageOverrideOptions baseImageOverrideOptions,
        ManifestInfo manifest,
        string? repoPrefix,
        string? sourceRepoPrefix)
    {
        _digestResolutionMode = digestResolutionMode;
        _baseImageOverrideOptions = baseImageOverrideOptions;
        _manifest = manifest;
        _repoPrefix = repoPrefix;
        _sourceRepoPrefix = sourceRepoPrefix;
    }

    /// <inheritdoc/>
    public string GetFromImageLocalTag(string fromImage) =>
        // Provides the overridable value of the registry (e.g. dotnetdocker.azurecr.io) because that is the registry that
        // would be used for tags that exist locally.
        GetFromImageTag(fromImage, _manifest.Registry);

    /// <inheritdoc/>
    public string GetFromImagePullTag(string fromImage) =>
        // Provides the raw registry value from the manifest (e.g. mcr.microsoft.com). This accounts for images that
        // are classified as external within the model but they are owned internally and not mirrored. An example of
        // this is sample images. By comparing their base image tag to that raw registry value from the manifest, we
        // can know that these are owned internally and not to attempt to pull them from the mirror location.
        GetFromImageTag(fromImage, _manifest.Model.Registry);

    /// <inheritdoc/>
    /// <remarks>
    /// This compares the registry of the image tag to determine if it's internally owned. If so, it returns
    /// the tag using the raw (non-overridden) registry from the manifest (e.g. mcr.microsoft.com). Otherwise,
    /// it returns the image tag unchanged.
    /// </remarks>
    public string GetFromImagePublicTag(string fromImage)
    {
        string trimmed = TrimInternallyOwnedRegistryAndRepoPrefix(fromImage);
        if (trimmed == fromImage)
        {
            return _baseImageOverrideOptions.ApplyBaseImageOverride(trimmed);
        }
        else
        {
            return $"{_manifest.Model.Registry}/{trimmed}";
        }
    }

    /// <inheritdoc/>
    public string GetFinalStageImageNameForDigestQuery(PlatformInfo platform)
    {
        string imageName = platform.FinalStageFromImage ?? string.Empty;

        if (platform.IsInternalFromImage(imageName))
        {
            return _digestResolutionMode switch
            {
                // For build scenarios, the image is already formatted with the staging ACR registry and repo prefix
                // (e.g. dotnetdocker.azurecr.io/dotnet-staging/12345/sdk:8.0), so use it as-is.
                DigestResolutionMode.Staging => imageName,

                // For matrix generation scenarios, strip the staging prefix and re-prefix with the public MCR
                // registry (e.g. mcr.microsoft.com/dotnet/sdk:8.0).
                DigestResolutionMode.Public =>
                    $"{_manifest.Model.Registry}/{TrimInternallyOwnedRegistryAndRepoPrefix(DockerHelper.NormalizeRepo(imageName))}",

                _ => throw new NotSupportedException($"Unsupported digest resolution mode: {_digestResolutionMode}")
            };
        }

        // External images are formatted to target the mirror location in the ACR
        // (e.g. dotnetdocker.azurecr.io/mirror/amd64/alpine:3.XX).
        return GetFromImagePullTag(imageName);
    }

    /// <summary>
    /// Gets the tag to use for the image of a FROM instruction.
    /// </summary>
    /// <param name="fromImage">Tag of the FROM image.</param>
    /// <param name="registry">Registry to use for comparing against the tag to determine if it's owned internally or external.</param>
    /// <remarks>
    /// This is meant to provide support for external images that need to be pulled from the mirror location.
    /// </remarks>
    private string GetFromImageTag(string fromImage, string? registry)
    {
        fromImage = _baseImageOverrideOptions.ApplyBaseImageOverride(fromImage);

        if (IsInRegistry(fromImage, registry)
            || IsInRegistry(fromImage, _manifest.Model.Registry)
            || _sourceRepoPrefix is null)
        {
            return fromImage;
        }

        string srcImage = TrimInternallyOwnedRegistryAndRepoPrefix(DockerHelper.NormalizeRepo(fromImage));
        return $"{_manifest.Registry}/{_sourceRepoPrefix}{srcImage}";
    }

    private string TrimInternallyOwnedRegistryAndRepoPrefix(string imageTag) =>
        IsInInternallyOwnedRegistry(imageTag) ?
            DockerHelper.TrimRegistry(imageTag).TrimStartString(_repoPrefix ?? string.Empty) :
            imageTag;

    private bool IsInInternallyOwnedRegistry(string imageTag) =>
        IsInRegistry(imageTag, _manifest.Registry) ||
        IsInRegistry(imageTag, _manifest.Model.Registry);

    private static bool IsInRegistry(string imageReference, string? registry) =>
        !string.IsNullOrEmpty(registry) && imageReference.StartsWith(registry);
}
