// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.RateLimiting;

/// <summary>
/// Throttles outbound HTTP requests to Azure Container Registry (ACR) hosts so that the shared ACR
/// request-rate limit is not exceeded. Requests to non-ACR hosts pass through without consuming a
/// permit.
/// </summary>
/// <remarks>
/// The rate-limiting state lives in the shared <see cref="AcrRateLimiter"/> singleton rather than
/// in this (transient) handler. <see cref="IHttpClientFactory"/> rebuilds handler chains
/// periodically; keeping the window in the singleton ensures the global cap holds across those
/// rebuilds.
/// </remarks>
public sealed class AcrRateLimitingHandler(AcrRateLimiter rateLimiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        bool isAcrRequest =
            request.RequestUri is { Host: string host }
            && host.EndsWith(".azurecr.io", StringComparison.OrdinalIgnoreCase);

        if (!isAcrRequest)
            return await base.SendAsync(request, cancellationToken);

        using RateLimitLease lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
            throw new InvalidOperationException("Unable to acquire a permit from the ACR rate limiter.");

        return await base.SendAsync(request, cancellationToken);
    }
}
