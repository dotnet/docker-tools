// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Service for signing container images and pushing signatures to the registry.
/// </summary>
public interface IBulkImageSigningService
{
    /// <summary>
    /// Signs multiple images in bulk by signing payloads via ESRP and pushing signature artifacts to the registry.
    /// </summary>
    /// <param name="requests">Signing requests containing image references and payloads.</param>
    /// <param name="signingKeyCode">Certificate ID used by DDSignFiles.dll.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results containing the digest of each signature artifact pushed to the registry.</returns>
    Task<IReadOnlyList<ImageSigningResult>> SignImagesAsync(
        IEnumerable<ImageSigningRequest> requests,
        int signingKeyCode,
        CancellationToken cancellationToken = default);
}
