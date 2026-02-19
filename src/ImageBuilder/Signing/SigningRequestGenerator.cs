// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.Extensions.Logging;

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
    private async Task<ImageSigningRequest> CreateSigningRequestAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching descriptor for {Reference}", reference);

        var descriptor = await _descriptorService.GetDescriptorAsync(reference, cancellationToken);

        var payload = new Payload(new Models.Oci.Descriptor(
            descriptor.MediaType,
            descriptor.Digest,
            descriptor.Size));

        return new ImageSigningRequest(reference, payload);
    }
}
