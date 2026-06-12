// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Microsoft.DotNet.ImageBuilder.RateLimiting;

/// <summary>
/// Azure SDK pipeline policy that throttles outbound requests through the shared <see cref="AcrRateLimiter"/>.
/// </summary>
/// <remarks>
/// Requests to non-ACR hosts do not have rate limiting applied.
/// </remarks>
/// <seealso cref="AcrRateLimitingHandler"/>
public sealed class AcrRateLimitingPolicy(AcrRateLimiter rateLimiter) : HttpPipelinePolicy
{
    public override async ValueTask ProcessAsync(
        HttpMessage message,
        ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        string requestUriHost = message.Request.Uri.ToUri().Host;
        bool isAcrRequest = requestUriHost.EndsWith(".azurecr.io", StringComparison.OrdinalIgnoreCase);

        if (!isAcrRequest)
        {
            await ProcessNextAsync(message, pipeline);
            return;
        }

        using RateLimitLease lease = await rateLimiter.AcquireAsync(message.CancellationToken);

        if (!lease.IsAcquired)
            throw new InvalidOperationException("Unable to acquire a permit from the ACR rate limiter.");

        await ProcessNextAsync(message, pipeline);
    }

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline) =>
        ProcessAsync(message, pipeline).AsTask().GetAwaiter().GetResult();
}
