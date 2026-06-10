// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.RateLimiting;

/// <summary>
/// Helpers for bounding the concurrency of fan-out operations that issue ACR data-plane
/// requests (e.g. ORAS referrer lookups, manifest reads, annotations).
/// </summary>
internal static class AcrParallelism
{
    /// <summary>
    /// Maximum number of concurrent in-flight ACR requests.
    /// </summary>
    /// <remarks>
    /// <see cref="AcrRateLimiter"/> already handles hard rate limiting for ACR requests.
    /// However, flooding the limiter with thousands of simultaneous permit-waiters hurts
    /// throughput. Keeping a small number of active requests in flight at once keeps pipeline
    /// saturated at the rate limiter target throughput. Testing indicated that bounded parallelism
    /// allows about 4x more request throughput without tripping rate limits.
    /// </remarks>
    private const int MaxDegreeOfParallelism = 8;

    /// <summary>
    /// Creates bounded <see cref="ParallelOptions"/> that ensure ACR requests/rate limiting is not overloaded.
    /// </summary>
    public static ParallelOptions CreateOptions(CancellationToken cancellationToken = default) =>
        new()
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            CancellationToken = cancellationToken,
        };
}
