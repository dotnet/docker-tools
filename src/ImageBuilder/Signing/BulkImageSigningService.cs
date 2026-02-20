// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Service for signing container images and pushing signatures to the registry.
/// </summary>
public class BulkImageSigningService(
    IPayloadSigningService payloadSigningService,
    IOrasSignatureService signatureService,
    ILogger<BulkImageSigningService> logger) : IBulkImageSigningService
{
    private readonly IPayloadSigningService _payloadSigningService = payloadSigningService;
    private readonly IOrasSignatureService _signatureService = signatureService;
    private readonly ILogger<BulkImageSigningService> _logger = logger;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ImageSigningResult>> SignImagesAsync(
        IEnumerable<ImageSigningRequest> requests,
        int signingKeyCode,
        CancellationToken cancellationToken = default)
    {
        List<ImageSigningRequest> requestList = requests.ToList();
        if (requestList.Count == 0) return [];

        _logger.LogInformation("Signing {Count} images...", requestList.Count);

        // Step 1: Sign all payloads via ESRP
        IReadOnlyList<PayloadSigningResult> signedPayloads =
            await _payloadSigningService.SignPayloadsAsync(requestList, signingKeyCode, cancellationToken);

        // Step 2: Push signatures to registry
        ConcurrentBag<ImageSigningResult> results = [];
        await Parallel.ForEachAsync(signedPayloads, cancellationToken, async (signedPayload, ct) =>
        {
            string signatureDigest =
                await _signatureService.PushSignatureAsync(signedPayload.Descriptor, signedPayload, ct);

            var result = new ImageSigningResult(signedPayload.ImageName, signatureDigest);
            results.Add(result);
        });

        _logger.LogInformation("Successfully signed {Count} images.", results.Count);
        return results.ToList();
    }
}
