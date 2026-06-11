// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.RateLimiting;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class AcrReferrerRateLimitingTests
{
    private const string ReferrerUrl = "https://myregistry.azurecr.io/v2/repo/referrers/sha256:abc";
    private const string NonReferrerAcrUrl = "https://myregistry.azurecr.io/v2/repo/manifests/latest";
    private const string NonAcrUrl = "https://status.mscr.io/api/onboardingstatus";

    [TestMethod]
    public async Task NonAcrRequests_AreNotRateLimited()
    {
        using AcrReferrerRateLimiter limiter = CreateSinglePermitLimiter();
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        // The limiter only has a single permit per host, but non-ACR requests must never consume it.
        for (int i = 0; i < 5; i++)
        {
            using HttpResponseMessage response = await SendAsync(invoker, NonAcrUrl);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        inner.RequestCount.ShouldBe(5);
    }

    [TestMethod]
    public async Task NonReferrerAcrRequests_AreNotRateLimited()
    {
        using AcrReferrerRateLimiter limiter = CreateSinglePermitLimiter();
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        // ACR requests that aren't referrer lookups rely on the resilience pipeline, not this limiter.
        for (int i = 0; i < 5; i++)
        {
            using HttpResponseMessage response = await SendAsync(invoker, NonReferrerAcrUrl);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        inner.RequestCount.ShouldBe(5);
    }

    [TestMethod]
    public async Task AcrRequestsToRepositoryWithReferrersSegment_AreNotRateLimited()
    {
        using AcrReferrerRateLimiter limiter = CreateSinglePermitLimiter();
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        using HttpResponseMessage firstResponse =
            await SendAsync(invoker, "https://myregistry.azurecr.io/v2/repo/referrers/app/manifests/latest");
        using HttpResponseMessage secondResponse =
            await SendAsync(invoker, "https://myregistry.azurecr.io/v2/repo/referrers/app/manifests/latest");

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.RequestCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task ReferrerRequest_ConsumesPermit()
    {
        using AcrReferrerRateLimiter limiter = CreateSinglePermitLimiter();
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        using HttpResponseMessage response = await SendAsync(invoker, ReferrerUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.RequestCount.ShouldBe(1);
    }

    [TestMethod]
    public async Task ReferrerRequest_IsThrottledWhenPermitsExhausted()
    {
        using AcrReferrerRateLimiter limiter = CreateSinglePermitLimiter();
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        using HttpResponseMessage firstResponse = await SendAsync(invoker, ReferrerUrl);

        // The window's single permit is now consumed; the next referrer request cannot be served.
        await Should.ThrowAsync<InvalidOperationException>(() => SendAsync(invoker, ReferrerUrl));

        inner.RequestCount.ShouldBe(1);
    }

    [TestMethod]
    public async Task ReferrerRequests_AreRateLimitedPerHost()
    {
        using AcrReferrerRateLimiter limiter = CreateSinglePermitLimiter();
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        // Exhaust the first registry's single permit.
        using HttpResponseMessage firstResponse = await SendAsync(invoker, ReferrerUrl);
        await Should.ThrowAsync<InvalidOperationException>(() => SendAsync(invoker, ReferrerUrl));

        // A different registry has its own independent limiter and must still be served.
        using HttpResponseMessage otherResponse =
            await SendAsync(invoker, "https://otherregistry.azurecr.io/v2/repo/referrers/sha256:def");
        otherResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        inner.RequestCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task ConcurrentReferrerRequestsForNewHost_CreateSingleLimiter()
    {
        int createdLimiterCount = 0;
        using AcrReferrerRateLimiter limiter = new(_ =>
        {
            Interlocked.Increment(ref createdLimiterCount);
            Thread.Sleep(TimeSpan.FromMilliseconds(50));
            return CreateSlidingWindowLimiter(permitLimit: 100);
        });
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task[] requests = Enumerable.Range(0, 25)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task;
                using HttpResponseMessage response = await SendAsync(invoker, ReferrerUrl);
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }))
            .ToArray();

        start.SetResult();
        await Task.WhenAll(requests);

        createdLimiterCount.ShouldBe(1);
        inner.RequestCount.ShouldBe(25);
    }

    [TestMethod]
    public void ResolvePermitLimit_UsesConfiguredValue()
    {
        PublishConfiguration config = new()
        {
            RegistryAuthentication =
            [
                new RegistryAuthentication
                {
                    Server = "myregistry.azurecr.io",
                    ReferrerRequestsPerMinute = 1000,
                }
            ]
        };

        AcrReferrerRateLimiter.ResolvePermitLimit(config, "myregistry.azurecr.io").ShouldBe(1000);
    }

    [TestMethod]
    public void ResolvePermitLimit_FallsBackToDefaultWhenUnset()
    {
        PublishConfiguration config = new()
        {
            RegistryAuthentication =
            [
                new RegistryAuthentication { Server = "myregistry.azurecr.io" }
            ]
        };

        AcrReferrerRateLimiter.ResolvePermitLimit(config, "myregistry.azurecr.io")
            .ShouldBe(AcrReferrerRateLimiter.DefaultReferrerRequestsPerMinute);
    }

    [TestMethod]
    public void ResolvePermitLimit_FallsBackToDefaultForUnknownRegistry()
    {
        PublishConfiguration config = new();

        AcrReferrerRateLimiter.ResolvePermitLimit(config, "unknown.azurecr.io")
            .ShouldBe(AcrReferrerRateLimiter.DefaultReferrerRequestsPerMinute);
    }

    [TestMethod]
    public async Task PublishConfigConstructor_AppliesPerRegistryLimit()
    {
        // Configure a single-permit-per-minute limit for the registry and verify a second referrer
        // lookup blocks (waits for the window), proving the configured limit is honored.
        PublishConfiguration config = new()
        {
            RegistryAuthentication =
            [
                new RegistryAuthentication
                {
                    Server = "myregistry.azurecr.io",
                    ReferrerRequestsPerMinute = 1,
                }
            ]
        };

        using AcrReferrerRateLimiter limiter = new(Options.Create(config));
        RecordingHandler inner = new();
        using HttpMessageInvoker invoker = CreateInvoker(limiter, inner);

        using HttpResponseMessage firstResponse = await SendAsync(invoker, ReferrerUrl);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The only permit for the window is consumed; the next request waits and is cancelled.
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => SendAsync(invoker, ReferrerUrl, cts.Token));

        inner.RequestCount.ShouldBe(1);
    }

    private static HttpMessageInvoker CreateInvoker(
        AcrReferrerRateLimiter limiter,
        HttpMessageHandler inner) =>
        new(new AcrReferrerRateLimitingHandler(limiter) { InnerHandler = inner });

    private static Task<HttpResponseMessage> SendAsync(
        HttpMessageInvoker invoker,
        string url,
        CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Get, url);
        return invoker.SendAsync(request, cancellationToken);
    }

    // Each host gets its own limiter with a single permit and no queue, so an exhausted window
    // yields a non-acquired lease (the handler then throws).
    private static AcrReferrerRateLimiter CreateSinglePermitLimiter() =>
        new(_ => CreateSlidingWindowLimiter(permitLimit: 1));

    private static SlidingWindowRateLimiter CreateSlidingWindowLimiter(int permitLimit) =>
        new(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(10),
            SegmentsPerWindow = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
