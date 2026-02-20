// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Microsoft.DotNet.ImageBuilder.Signing.CertificateChainCalculator;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Service for signing Notary v2 payloads via ESRP.
/// </summary>
public class PayloadSigningService(
    IEsrpSigningService esrpSigningService,
    ILogger<PayloadSigningService> logger,
    IFileSystem fileSystem,
    IOptions<BuildConfiguration> buildConfigOptions) : IPayloadSigningService
{
    private const string SigningPayloadsSubdirectory = "signing-payloads";

    private readonly IEsrpSigningService _esrpSigningService = esrpSigningService;
    private readonly ILogger<PayloadSigningService> _logger = logger;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly BuildConfiguration _buildConfig = buildConfigOptions.Value;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PayloadSigningResult>> SignPayloadsAsync(
        IEnumerable<ImageSigningRequest> requests,
        int signingKeyCode,
        CancellationToken cancellationToken = default)
    {
        List<ImageSigningRequest> requestList = requests.ToList();
        if (requestList.Count == 0)
        {
            _logger.LogWarning("No signing requests provided for signing.");
            return [];
        }

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
        List<WrittenPayload> writtenPayloads = [];
        foreach (ImageSigningRequest request in requestList)
        {
            string digest = request.Payload.TargetArtifact.Digest;
            string safeFilename = SanitizeDigestForFilename(digest) + ".payload";
            string payloadFilePath = Path.Combine(payloadDirectory.FullName, safeFilename);
            string payloadJson = request.Payload.ToJson();

            // Write synchronously because payload files are small (~<1KB).
            // Parallelizing is not faster for files this small.
            _fileSystem.WriteAllText(payloadFilePath, payloadJson);
            writtenPayloads.Add(new WrittenPayload(request, payloadFilePath));

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
    /// Converts a digest like "sha256:abc123..." to a safe filename like "sha256-abc123...".
    /// </summary>
    private static string SanitizeDigestForFilename(string digest) => digest.Replace(":", "-");
}
