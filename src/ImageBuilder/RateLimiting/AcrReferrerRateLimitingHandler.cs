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
    private const string RegistryApiVersionPathSegment = "v2";
    private const string ReferrersPathSegment = "referrers";

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

    private static bool IsAcrReferrerLookup(HttpRequestMessage request)
    {
        if (request.RequestUri is not { Host: string host } uri
            || !host.EndsWith(".azurecr.io", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return pathSegments.Length >= 4
            && string.Equals(pathSegments[0], RegistryApiVersionPathSegment, StringComparison.Ordinal)
            && string.Equals(pathSegments[^2], ReferrersPathSegment, StringComparison.OrdinalIgnoreCase);
    }
}
