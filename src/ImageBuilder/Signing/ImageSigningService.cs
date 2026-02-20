// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Microsoft.DotNet.ImageBuilder.Signing.CertificateChainCalculator;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Service for signing container images and pushing signatures to the registry.
/// Resolves OCI descriptors, signs payloads via ESRP, and pushes signature artifacts.
/// </summary>
public class ImageSigningService(
    IOrasService orasService,
    IEsrpSigningService esrpSigningService,
    ILogger<ImageSigningService> logger,
    IFileSystem fileSystem,
    IOptions<BuildConfiguration> buildConfigOptions) : IImageSigningService
{
    private const string SigningPayloadsSubdirectory = "signing-payloads";

    private readonly IOrasService _orasService = orasService;
    private readonly IEsrpSigningService _esrpSigningService = esrpSigningService;
    private readonly ILogger<ImageSigningService> _logger = logger;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly BuildConfiguration _buildConfig = buildConfigOptions.Value;

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
            await SignPayloadsAsync(requests, signingKeyCode, cancellationToken);

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
    /// Signs Notary v2 payloads by writing them to disk, invoking ESRP, and calculating certificate chains.
    /// </summary>
    private async Task<IReadOnlyList<PayloadSigningResult>> SignPayloadsAsync(
        IEnumerable<ImageSigningRequest> requests,
        int signingKeyCode,
        CancellationToken cancellationToken)
    {
        List<ImageSigningRequest> requestList = requests.ToList();
        if (requestList.Count == 0) return [];

        if (string.IsNullOrEmpty(_buildConfig.ArtifactStagingDirectory))
        {
            throw new InvalidOperationException(
                $"{nameof(BuildConfiguration.ArtifactStagingDirectory)} is not set. " +
                "Configure it in appsettings.json or via environment variables.");
        }

        string payloadDirectoryPath = Path.Combine(_buildConfig.ArtifactStagingDirectory, SigningPayloadsSubdirectory);
        DirectoryInfo payloadDirectory = _fileSystem.CreateDirectory(payloadDirectoryPath);

        _logger.LogInformation(
            "Writing {NumberOfPayloads} payloads to {Directory}",
            requestList.Count, payloadDirectory.FullName);

        // Write all payloads to disk
        List<(ImageSigningRequest Request, string PayloadFilePath)> writtenPayloads = [];
        foreach (ImageSigningRequest request in requestList)
        {
            string digest = request.Payload.TargetArtifact.Digest;
            string safeFilename = digest.Replace(":", "-") + ".payload";
            string payloadFilePath = Path.Combine(payloadDirectory.FullName, safeFilename);
            string payloadJson = request.Payload.ToJson();

            // Write synchronously because payload files are small (~<1KB).
            _fileSystem.WriteAllText(payloadFilePath, payloadJson);
            writtenPayloads.Add((request, payloadFilePath));

            _logger.LogInformation(
                "Wrote payload for {ImageName} to {Filename}",
                request.ImageName, safeFilename);
        }

        // Sign all files
        IEnumerable<string> allPayloadFiles = writtenPayloads.Select(wp => wp.PayloadFilePath);
        await _esrpSigningService.SignFilesAsync(allPayloadFiles, signingKeyCode, cancellationToken);

        // Calculate certificate chains and build results
        var results = writtenPayloads
            .Select(written => new PayloadSigningResult(
                ImageName: written.Request.ImageName,
                Descriptor: written.Request.Descriptor,
                SignedPayloadFilePath: written.PayloadFilePath,
                // Theoretically, all images signed with the same key should have the same cert chain. However, the
                // cert chain is determined entirely by the signature envelope returned to us by ESRP. To be safe, we
                // calculate the cert chain for each payload individually rather than assuming they are all the same.
                // ESRP could return different signature envelopes for different payloads if certs are rotated
                // mid-signing, or for any other reason.
                CertificateChain: CalculateCertificateChainThumbprints(written.PayloadFilePath, _fileSystem)))
            .ToList();

        return results;
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
