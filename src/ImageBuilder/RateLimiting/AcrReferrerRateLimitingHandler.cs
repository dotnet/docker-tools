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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!IsAcrReferrerLookup(request))
            return await base.SendAsync(request, ct);

        using RateLimitLease lease = await rateLimiter.AcquireAsync(request.RequestUri!.Host, ct);

        if (!lease.IsAcquired)
            throw new InvalidOperationException("Unable to acquire a permit from the ACR referrer rate limiter.");

        return await base.SendAsync(request, ct);
    }

    private static bool IsAcrReferrerLookup(HttpRequestMessage request)
    {
        bool isAcrRequest =
            request.RequestUri is { Host: string host }
            && host.EndsWith(".azurecr.io", StringComparison.OrdinalIgnoreCase);

        if (!isAcrRequest)
            return false;

        string[] pathSegments =
            request.RequestUri?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];

        // Match only the OCI referrers API:
        // /v2/repo/referrers/sha256:abc
        // /v2/nested/repo/referrers/sha256:abc

        // Do not match repositories that contain a "referrers" segment in the repo name:
        // /v2/repo/referrers/app/manifests/latest

        bool hasMinimumReferrersPathSegments = pathSegments.Length >= 4;

        bool usesV2RegistryApi =
            hasMinimumReferrersPathSegments
            && string.Equals(pathSegments[0], RegistryApiVersionPathSegment, StringComparison.Ordinal);

        bool hasReferrersEndpointSegment =
            hasMinimumReferrersPathSegments
            && string.Equals(pathSegments[^2], ReferrersPathSegment, StringComparison.OrdinalIgnoreCase);

        return hasMinimumReferrersPathSegments && usesV2RegistryApi && hasReferrersEndpointSegment;
    }
}
