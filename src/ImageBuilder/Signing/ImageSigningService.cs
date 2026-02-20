// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.Extensions.Logging;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Service for signing container images and pushing signatures to the registry.
/// Resolves OCI descriptors, signs payloads via ESRP, and pushes signature artifacts.
/// </summary>
public class ImageSigningService(
    IOrasService orasService,
    IPayloadSigningService payloadSigningService,
    ILogger<ImageSigningService> logger) : IImageSigningService
{
    private readonly IOrasService _orasService = orasService;
    private readonly IPayloadSigningService _payloadSigningService = payloadSigningService;
    private readonly ILogger<ImageSigningService> _logger = logger;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ImageSigningResult>> SignImagesAsync(
        ImageArtifactDetails imageArtifactDetails,
        int signingKeyCode,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> imageDigests = ExtractAllImageDigests(imageArtifactDetails);
        if (imageDigests.Count == 0) return [];

        _logger.LogInformation("Signing {Count} digests.", imageDigests.Count);

        // Step 1: Generate signing requests by fetching OCI descriptors in parallel
        _logger.LogInformation("Generating {Count} signing requests.", imageDigests.Count);
        ConcurrentBag<ImageSigningRequest> requests = [];
        await Parallel.ForEachAsync(imageDigests, cancellationToken, async (imageDigest, ct) =>
        {
            OrasDescriptor descriptor = await _orasService.GetDescriptorAsync(imageDigest, ct);
            ImageSigningRequest request = ConstructSigningRequest(imageDigest, descriptor);
            requests.Add(request);
        });

        // Step 2: Sign all payloads via ESRP
        _logger.LogInformation("Signing {Count} images.", requests.Count);
        IReadOnlyList<PayloadSigningResult> signedPayloads =
            await _payloadSigningService.SignPayloadsAsync(requests, signingKeyCode, cancellationToken);

        // Step 3: Push signatures to registry in parallel
        _logger.LogInformation("Pushing {Count} signatures to registry.", signedPayloads.Count);
        ConcurrentBag<ImageSigningResult> results = [];
        await Parallel.ForEachAsync(signedPayloads, cancellationToken, async (signedPayload, ct) =>
        {
            string signatureDigest =
                await _orasService.PushSignatureAsync(signedPayload.Descriptor, signedPayload, ct);
            ImageSigningResult result = new(signedPayload.ImageName, signatureDigest);
            results.Add(result);
        });

        _logger.LogInformation("Signed {Count} digests.", results.Count);
        return results.ToList();
    }

    /// <summary>
    /// Extracts all digest references (platform manifests and manifest lists) from the artifact details.
    /// </summary>
    private static IReadOnlyList<string> ExtractAllImageDigests(ImageArtifactDetails imageArtifactDetails)
    {
        IEnumerable<string> platformDigests = imageArtifactDetails.Repos
            .SelectMany(repo => repo.Images
                .SelectMany(image => image.Platforms
                    .Where(platform => !string.IsNullOrEmpty(platform.Digest))
                    .Select(platform => platform.Digest)));

        IEnumerable<string> manifestListDigests = imageArtifactDetails.Repos
            .SelectMany(repo => repo.Images
                .Where(image => image.Manifest is not null && !string.IsNullOrEmpty(image.Manifest.Digest))
                .Select(image => image.Manifest.Digest));

        return [..platformDigests, ..manifestListDigests];
    }

    /// <summary>
    /// Builds an <see cref="ImageSigningRequest"/> from a reference and its resolved OCI descriptor.
    /// </summary>
    private static ImageSigningRequest ConstructSigningRequest(string imageDigest, OrasDescriptor descriptor)
    {
        var ociDescriptor = new Models.Oci.Descriptor(
            MediaType: descriptor.MediaType,
            Digest: descriptor.Digest,
            Size: descriptor.Size);

        var payload = new Payload(TargetArtifact: ociDescriptor);

        return new ImageSigningRequest(
            ImageName: imageDigest,
            Descriptor: descriptor,
            Payload: payload);
    }
}
