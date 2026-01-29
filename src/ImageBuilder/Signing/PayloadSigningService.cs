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
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Service for signing Notary v2 payloads via ESRP.
/// </summary>
public class PayloadSigningService(
    IEsrpSigningService esrpSigningService,
    ILoggerService logger,
    IOptions<BuildConfiguration> buildConfigOptions) : IPayloadSigningService
{
    private const string SigningPayloadsSubdirectory = "signing-payloads";

    private readonly IEsrpSigningService _esrpSigningService = esrpSigningService;
    private readonly ILoggerService _logger = logger;
    private readonly BuildConfiguration _buildConfig = buildConfigOptions.Value;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PayloadSigningResult>> SignPayloadsAsync(
        IEnumerable<ImageSigningRequest> requests,
        int signingKeyCode,
        CancellationToken cancellationToken = default)
    {
        var requestList = requests.ToList();
        if (requestList.Count == 0)
        {
            return [];
        }

        var payloadDirectory = GetPayloadDirectory();
        _logger.WriteMessage($"Writing {requestList.Count} payloads to {payloadDirectory.FullName}");

        // Write all payloads to disk
        var payloadFiles = WritePayloadsToDisk(requestList, payloadDirectory);

        // Sign all files
        await _esrpSigningService.SignFilesAsync(
            payloadFiles.Select(f => f.FullName),
            signingKeyCode,
            cancellationToken);

        // Calculate certificate chains and build results
        var results = new List<PayloadSigningResult>();
        for (var i = 0; i < requestList.Count; i++)
        {
            var request = requestList[i];
            var signedFile = payloadFiles[i];

            var certChain = CertificateChainCalculator.CalculateCertificateChainThumbprints(signedFile.FullName);

            results.Add(new PayloadSigningResult(request.ImageName, signedFile, certChain));
        }

        return results;
    }

    /// <summary>
    /// Gets or creates the directory for signing payloads.
    /// </summary>
    private DirectoryInfo GetPayloadDirectory()
    {
        if (string.IsNullOrEmpty(_buildConfig.ArtifactStagingDirectory))
        {
            throw new InvalidOperationException(
                "BuildConfiguration.ArtifactStagingDirectory is not set. " +
                "Configure it in appsettings.json or via environment variables.");
        }

        var payloadDir = Path.Combine(_buildConfig.ArtifactStagingDirectory, SigningPayloadsSubdirectory);
        return Directory.CreateDirectory(payloadDir);
    }

    /// <summary>
    /// Writes all payloads to disk with filenames based on their digest.
    /// </summary>
    private List<FileInfo> WritePayloadsToDisk(List<ImageSigningRequest> requests, DirectoryInfo directory)
    {
        var files = new List<FileInfo>();

        foreach (var request in requests)
        {
            // Use digest from payload's target artifact for filename
            var digest = request.Payload.TargetArtifact.Digest;
            var safeFilename = SanitizeDigestForFilename(digest) + ".payload";
            var filePath = Path.Combine(directory.FullName, safeFilename);

            File.WriteAllText(filePath, request.Payload.ToJson());
            files.Add(new FileInfo(filePath));

            _logger.WriteMessage($"Wrote payload for {request.ImageName} to {safeFilename}");
        }

        return files;
    }

    /// <summary>
    /// Converts a digest like "sha256:abc123..." to a safe filename like "sha256-abc123...".
    /// </summary>
    private static string SanitizeDigestForFilename(string digest)
    {
        return digest.Replace(":", "-");
    }
}
