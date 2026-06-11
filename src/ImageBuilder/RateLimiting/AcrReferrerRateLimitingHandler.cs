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
/// Throttles outbound OCI referrer-lookup requests to Azure Container Registry (ACR) hosts. Only
/// requests to the referrers endpoint (<c>/v2/&lt;repo&gt;/referrers/&lt;digest&gt;</c>) on a
/// <c>*.azurecr.io</c> host are rate limited; all other requests pass through untouched and rely
/// on the HTTP resilience pipeline (retry + Retry-After).
/// </summary>
public sealed class AcrReferrerRateLimitingHandler(AcrReferrerRateLimiter rateLimiter) : DelegatingHandler
{
    private const string ReferrersPathSegment = "/referrers/";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!IsAcrReferrerLookup(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        using RateLimitLease lease =
            await rateLimiter.AcquireAsync(request.RequestUri!.Host, cancellationToken);

        if (!lease.IsAcquired)
            throw new InvalidOperationException("Unable to acquire a permit from the ACR referrer rate limiter.");

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool IsAcrReferrerLookup(HttpRequestMessage request) =>
        request.RequestUri is { Host: string host } uri
        && host.EndsWith(".azurecr.io", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Contains(ReferrersPathSegment, StringComparison.OrdinalIgnoreCase);
}
