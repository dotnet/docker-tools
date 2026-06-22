// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ImageBuilder;

/// <inheritdoc/>
public class ImageInfoService(
    IManifestJsonService manifestJsonService,
    IOrasServiceFactory orasServiceFactory,
    ILogger<ImageInfoService> logger
) : IImageInfoService
{
    private readonly IManifestJsonService _manifestJsonService =
        manifestJsonService ?? throw new ArgumentNullException(nameof(manifestJsonService));

    private readonly IOrasServiceFactory _orasServiceFactory =
        orasServiceFactory ?? throw new ArgumentNullException(nameof(orasServiceFactory));

    private readonly ILogger<ImageInfoService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task PushImageInfoArtifactAsync(
        ManifestInfo manifest,
        byte[] imageInfoContent,
        string registry,
        string? repoPrefix,
        bool isDryRun,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageInfoContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(registry);
        ArgumentNullException.ThrowIfNull(manifest);

        ImageInfoArtifact imageInfoArtifact = GetValidatedImageInfoArtifact(manifest);

        string repo = $"{repoPrefix}{imageInfoArtifact.Repo}";
        _logger.LogInformation(
            "Image info will be published to registry={registry}, repo={Repo}, dryRun={DryRun}",
            registry, repo, isDryRun);

        if (isDryRun)
        {
            _logger.LogInformation("Skipping image info artifact push due to dry run.");
            return;
        }

        IOrasService orasService = _orasServiceFactory.Create();

        await orasService.PushArtifactAsync(
            imageInfoContent,
            OciArtifactType.ImageInfo,
            OciArtifactType.ImageInfo,
            registry,
            repo,
            imageInfoArtifact.Tags.Keys,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> PullImageInfoArtifactAsync(
        ManifestInfo manifest,
        string registry,
        string? repoPrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registry);
        ArgumentNullException.ThrowIfNull(manifest);

        ImageInfoArtifact imageInfoArtifact = GetValidatedImageInfoArtifact(manifest);

        string repo = $"{repoPrefix}{imageInfoArtifact.Repo}";
        string tag = imageInfoArtifact.Tags.Keys.First();

        _logger.LogInformation(
            "Image info will be pulled from registry={registry}, repo={Repo}, tag={Tag}",
            registry, repo, tag);

        IOrasService orasService = _orasServiceFactory.Create();
        OciArtifact artifact = await orasService.PullAsync(
            registry,
            repo,
            tag,
            cancellationToken);
        if (!string.Equals(artifact.Manifest.ArtifactType, OciArtifactType.ImageInfo, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Artifact '{repo}:{tag}' has artifactType '{artifact.Manifest.ArtifactType}', expected '{OciArtifactType.ImageInfo}'.");
        }

        if (artifact.Blobs.Count != 1)
        {
            throw new InvalidOperationException(
                $"Image info artifact '{repo}:{tag}' must have exactly one blob, but has {artifact.Blobs.Count}.");
        }

        OciBlob blob = artifact.Blobs[0];
        if (!string.Equals(blob.Descriptor.MediaType, OciArtifactType.ImageInfo, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Artifact '{repo}:{tag}' blob has mediaType '{blob.Descriptor.MediaType}', expected '{OciArtifactType.ImageInfo}'.");
        }

        return Encoding.UTF8.GetString(blob.Content);
    }

    private ImageInfoArtifact GetValidatedImageInfoArtifact(ManifestInfo manifest)
    {
        ImageInfoArtifact imageInfoArtifact = _manifestJsonService.GetImageInfoArtifact(manifest);

        if (imageInfoArtifact.Tags.Count == 0)
        {
            throw new InvalidOperationException(
                "The manifest's imageInfo property must define at least one tag in order to use "
                + "image-info as an OCI artifact.");
        }

        return imageInfoArtifact;
    }
}
