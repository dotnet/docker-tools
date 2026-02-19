// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    private readonly ILogger<SigningRequestGenerator> _logger;

    public SigningRequestGenerator(
        IOrasDescriptorService descriptorService,
        ILogger<SigningRequestGenerator> logger)
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
                    .Select(platform => platform.Digest)))
            .ToList();

        _logger.LogInformation("Generating signing requests for {Count} platform images.", platformReferences.Count);

        var requests = new List<ImageSigningRequest>();

        foreach (var reference in platformReferences)
        {
            _logger.LogInformation("Platform reference: {Reference}", reference);
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
                .Select(image => image.Manifest!.Digest))
            .ToList();

        _logger.LogInformation("Generating signing requests for {Count} manifest lists.", manifestReferences.Count);

        var requests = new List<ImageSigningRequest>();

        foreach (var reference in manifestReferences)
        {
            _logger.LogInformation("Manifest reference: {Reference}", reference);
            var request = await CreateSigningRequestAsync(reference, cancellationToken);
            requests.Add(request);
        }

        return requests;
    }

    /// <summary>
    /// Creates a signing request by fetching the OCI descriptor from the registry.
    /// </summary>
    /// <param name="imageReference">The image reference (digest) to fetch the descriptor for.</param>
    private async Task<ImageSigningRequest> CreateSigningRequestAsync(
        string imageReference,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching descriptor for {Reference}", imageReference);

        var descriptor = await _descriptorService.GetDescriptorAsync(imageReference, cancellationToken);

        var ociDescriptor = new Models.Oci.Descriptor(
            MediaType: descriptor.MediaType,
            Digest: descriptor.Digest,
            Size: descriptor.Size);

        var payload = new Payload(TargetArtifact: ociDescriptor);

        return new ImageSigningRequest(imageReference, descriptor, payload);
    }
}
