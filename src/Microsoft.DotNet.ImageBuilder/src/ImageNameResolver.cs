// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
internal class ImageNameResolver
{
    private readonly BaseImageOverrideOptions _baseImageOverrideOptions;
    private readonly ManifestInfo _manifest;
    private readonly string? _repoPrefix;
    private readonly string? _sourceRepoPrefix;

    public ImageNameResolver(BaseImageOverrideOptions baseImageOverrideOptions, ManifestInfo manifest, string? repoPrefix, string? sourceRepoPrefix)
    {
        _baseImageOverrideOptions = baseImageOverrideOptions;
        _manifest = manifest;
        _repoPrefix = repoPrefix;
        _sourceRepoPrefix = sourceRepoPrefix;
    }

    /// <summary>
    /// Returns the tag to use for interacting with the image of a FROM instruction that has been pulled or built locally.
    /// </summary>
    /// <param name="fromImage">Tag of the FROM image.</param>
    public string GetFromImageLocalTag(string fromImage) =>
        // Provides the overridable value of the registry (e.g. dotnetdocker.azurecr.io) because that is the registry that
        // would be used for tags that exist locally.
        GetFromImageTag(fromImage, _manifest.Registry);

    /// <summary>
    /// Returns the tag to use for pulling the image of a FROM instruction.
    /// </summary>
    /// <param name="fromImage">Tag of the FROM image.</param>
    public string GetFromImagePullTag(string fromImage) =>
        // Provides the raw registry value from the manifest (e.g. mcr.microsoft.com). This accounts for images that
        // are classified as external within the model but they are owned internally and not mirrored. An example of
        // this is sample images. By comparing their base image tag to that raw registry value from the manifest, we
        // can know that these are owned internally and not to attempt to pull them from the mirror location.
        GetFromImageTag(fromImage, _manifest.Model.Registry);

    /// <summary>
    /// Returns the tag that represents the publicly available tag of a FROM instruction.
    /// </summary>
    /// <param name="fromImage">Tag of the FROM image.</param>
    /// <remarks>
    /// This compares the registry of the image tag to determine if it's internally owned. If so, it returns
    /// the tag using the raw (non-overriden) registry from the manifest (e.g. mcr.microsoft.com). Otherwise,
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

        if ((registry is not null && DockerHelper.IsInRegistry(fromImage, registry)) ||
            DockerHelper.IsInRegistry(fromImage, _manifest.Model.Registry)
            || _sourceRepoPrefix is null)
        {
            return fromImage;
        }

        string srcImage = TrimInternallyOwnedRegistryAndRepoPrefix(DockerHelper.NormalizeRepo(fromImage));
        return $"{_manifest.Registry}/{_sourceRepoPrefix}{srcImage}";
    }

    private string TrimInternallyOwnedRegistryAndRepoPrefix(string imageTag) =>
        IsInInternallyOwnedRegistry(imageTag) ?
            DockerHelper.TrimRegistry(imageTag).TrimStart(_repoPrefix) :
            imageTag;

    private bool IsInInternallyOwnedRegistry(string imageTag) =>
        DockerHelper.IsInRegistry(imageTag, _manifest.Registry) ||
        DockerHelper.IsInRegistry(imageTag, _manifest.Model.Registry);
}
