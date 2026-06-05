// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.RateLimiting;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class AcrRateLimitingHandlerTests
{
    [Fact]
    public async Task NonAcrRequests_DoNotConsumePermits()
    {
        using AcrRateLimiter limiter = CreateSinglePermitLimiter();
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        // The limiter only has a single permit, but non-ACR requests must never consume it.
        for (int i = 0; i < 5; i++)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                "https://status.mscr.io/api/onboardingstatus"
            );
            using HttpResponseMessage response = await invoker.SendAsync(
                request,
                CancellationToken.None
            );
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        inner.RequestCount.ShouldBe(5);
    }

    [Fact]
    public async Task AcrRequest_ConsumesPermit()
    {
        using AcrRateLimiter limiter = CreateSinglePermitLimiter();
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "https://myregistry.azurecr.io/v2/"
        );
        using HttpResponseMessage response = await invoker.SendAsync(
            request,
            CancellationToken.None
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task AcrRequest_IsThrottledWhenPermitsExhausted()
    {
        using AcrRateLimiter limiter = CreateSinglePermitLimiter();
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        using HttpRequestMessage firstRequest = new(
            HttpMethod.Get,
            "https://myregistry.azurecr.io/v2/"
        );
        using HttpResponseMessage firstResponse = await invoker.SendAsync(
            firstRequest,
            CancellationToken.None
        );

        // The window's single permit is now consumed; the next ACR request cannot be served.
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            using HttpRequestMessage throttledRequest = new(
                HttpMethod.Get,
                "https://myregistry.azurecr.io/v2/"
            );
            await invoker.SendAsync(throttledRequest, CancellationToken.None);
        });

        inner.RequestCount.ShouldBe(1);
    }

    private static HttpMessageInvoker CreateInvoker(
        AcrRateLimiter limiter,
        HttpMessageHandler inner
    ) => new(new AcrRateLimitingHandler(limiter) { InnerHandler = inner });

    private static AcrRateLimiter CreateSinglePermitLimiter() =>
        new(
            new SlidingWindowRateLimiter(
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(10),
                    SegmentsPerWindow = 1,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }
            )
        );

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
