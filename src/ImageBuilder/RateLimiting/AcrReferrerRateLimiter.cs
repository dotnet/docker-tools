// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder.RateLimiting;

/// <summary>
/// Enforces Azure Container Registry's referrer-lookup rate limit (the /v2/repo/referrers/digest
/// endpoint) on a per-registry basis. A separate sliding-window limiter is maintained for each
/// registry host so that registries can be configured independently.
/// </summary>
/// <remarks>
/// Only referrer lookups are throttled here. All other ACR operations rely on the HTTP resilience
/// pipeline (retry + Retry-After). Register this as a singleton so the sliding windows are shared
/// by every caller.
/// </remarks>
public sealed class AcrReferrerRateLimiter : IDisposable
{
    /// <summary>
    /// The referrer-lookup rate limit used when a registry does not specify
    /// <see cref="RegistryAuthentication.ReferrerRequestsPerMinute"/>.
    /// </summary>
    public const int DefaultReferrerRequestsPerMinute = 250;

    private static readonly TimeSpan s_window = TimeSpan.FromSeconds(60);

    private readonly Func<string, RateLimiter> _limiterFactory;
    private readonly ConcurrentDictionary<string, RateLimiter> _limiters =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a rate limiter that resolves each registry's referrer-lookup limit from the publish
    /// configuration, falling back to <see cref="DefaultReferrerRequestsPerMinute"/> when unset.
    /// </summary>
    public AcrReferrerRateLimiter(IOptions<PublishConfiguration> publishConfig)
        : this(host => CreateSlidingWindowLimiter(ResolvePermitLimit(publishConfig.Value, host)))
    {
    }

    /// <summary>
    /// Creates a rate limiter with a custom per-host limiter factory. Intended for testing.
    /// </summary>
    public AcrReferrerRateLimiter(Func<string, RateLimiter> limiterFactory)
    {
        ArgumentNullException.ThrowIfNull(limiterFactory);
        _limiterFactory = limiterFactory;
    }

    /// <summary>
    /// Resolves the referrer-lookup permit limit (per 60-second window) for the given registry host,
    /// using the registry's configured <see cref="RegistryAuthentication.ReferrerRequestsPerMinute"/>
    /// or <see cref="DefaultReferrerRequestsPerMinute"/> when unset.
    /// </summary>
    public static int ResolvePermitLimit(PublishConfiguration publishConfig, string host) =>
        publishConfig.FindRegistryAuthentication(host)?.ReferrerRequestsPerMinute
            ?? DefaultReferrerRequestsPerMinute;

    /// <summary>
    /// Acquires a single permit for the given registry host, waiting until one is available within
    /// the current window.
    /// </summary>
    public ValueTask<RateLimitLease> AcquireAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        RateLimiter limiter = _limiters.GetOrAdd(host, _limiterFactory);
        return limiter.AcquireAsync(permitCount: 1, cancellationToken);
    }

    private static RateLimiter CreateSlidingWindowLimiter(int permitLimit) =>
        new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = s_window,
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
        });

    public void Dispose()
    {
        foreach (RateLimiter limiter in _limiters.Values)
        {
            limiter.Dispose();
        }
    }
}
