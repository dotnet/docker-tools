// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.RateLimiting;

/// <summary>
/// Enforces Azure Container Registry's request-rate limit across the whole process by wrapping a
/// single shared <see cref="RateLimiter"/>. Register this as a singleton so the sliding window is
/// shared by every caller.
/// </summary>
public sealed class AcrRateLimiter : IDisposable
{
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Creates a rate limiter for Azure Container Registry API calls. Defaults to the known ACR
    /// per-identity rate limit. The custom <paramref name="rateLimiter"/> parameter exists so that
    /// tests can inject a custom limiter.
    /// </summary>
    public AcrRateLimiter(RateLimiter? rateLimiter = null)
    {
        if (rateLimiter is not null)
        {
            _rateLimiter = rateLimiter;
            return;
        }

        // ACR allows 250 requests per 60 seconds per identity.
        // Going over that limit causes HTTP 429 errors.
        var rateLimiterOptions = new SlidingWindowRateLimiterOptions
        {
            // Stay slightly under the rate limit.
            PermitLimit = 240,
            Window = TimeSpan.FromSeconds(60),
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
        };

        _rateLimiter = new SlidingWindowRateLimiter(rateLimiterOptions);
    }

    /// <summary>
    /// Acquires a single permit, waiting until one is available within the current window.
    /// </summary>
    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken) =>
        _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);

    public void Dispose() => _rateLimiter.Dispose();
}
