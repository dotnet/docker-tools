// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Oras;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Generates signing requests from image artifact details by fetching OCI descriptors from the registry.
/// </summary>
public class SigningRequestGenerator : ISigningRequestGenerator
{
    private readonly IOrasDescriptorService _descriptorService;
    private readonly ILoggerService _logger;

    public SigningRequestGenerator(
        IOrasDescriptorService descriptorService,
        ILoggerService logger)
    {
        _descriptorService = descriptorService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ImageSigningRequest>> GeneratePlatformSigningRequestsAsync(
        ImageArtifactDetails imageArtifactDetails,
        CancellationToken cancellationToken = default)
    {
        var platformReferences = imageArtifactDetails.Repos
            .SelectMany(repo => repo.Images
                .SelectMany(image => image.Platforms
                    .Where(platform => !string.IsNullOrEmpty(platform.Digest))
                    .Select(platform => new
                    {
                        Repo = repo.Repo,
                        Digest = platform.Digest
                    })))
            .ToList();

        _logger.WriteMessage($"Generating signing requests for {platformReferences.Count} platform images.");

        var requests = new List<ImageSigningRequest>();

        foreach (var platform in platformReferences)
        {
            var reference = $"{platform.Repo}@{platform.Digest}";
            var request = await CreateSigningRequestAsync(reference, cancellationToken);
            requests.Add(request);
        }

        return requests;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ImageSigningRequest>> GenerateManifestListSigningRequestsAsync(
        ImageArtifactDetails imageArtifactDetails,
        CancellationToken cancellationToken = default)
    {
        var manifestReferences = imageArtifactDetails.Repos
            .SelectMany(repo => repo.Images
                .Where(image => image.Manifest is not null && !string.IsNullOrEmpty(image.Manifest.Digest))
                .Select(image => new
                {
                    Repo = repo.Repo,
                    Digest = image.Manifest!.Digest
                }))
            .ToList();

        _logger.WriteMessage($"Generating signing requests for {manifestReferences.Count} manifest lists.");

        var requests = new List<ImageSigningRequest>();

        foreach (var manifest in manifestReferences)
        {
            var reference = $"{manifest.Repo}@{manifest.Digest}";
            var request = await CreateSigningRequestAsync(reference, cancellationToken);
            requests.Add(request);
        }

        return requests;
    }

    /// <summary>
    /// Creates a signing request by fetching the OCI descriptor from the registry.
    /// </summary>
    private async Task<ImageSigningRequest> CreateSigningRequestAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        _logger.WriteMessage($"Fetching descriptor for {reference}");

        var descriptor = await _descriptorService.GetDescriptorAsync(reference, cancellationToken);

        var payload = new Payload(new Models.Oci.Descriptor(
            descriptor.MediaType,
            descriptor.Digest,
            descriptor.Size));

        return new ImageSigningRequest(reference, payload);
    }
}
