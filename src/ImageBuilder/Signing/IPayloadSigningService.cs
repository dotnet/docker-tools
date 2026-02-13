// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Service for signing Notary v2 payloads via ESRP.
/// </summary>
public interface IPayloadSigningService
{
    /// <summary>
    /// Signs payloads in bulk by writing them to disk, invoking ESRP, and calculating certificate chains.
    /// </summary>
    /// <param name="requests">Signing requests containing image references and payloads.</param>
    /// <param name="signingKeyCode">Certificate ID used by DDSignFiles.dll.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results containing signed payload files and certificate chains.</returns>
    Task<IReadOnlyList<PayloadSigningResult>> SignPayloadsAsync(
        IEnumerable<ImageSigningRequest> requests,
        int signingKeyCode,
        CancellationToken cancellationToken = default);
}
