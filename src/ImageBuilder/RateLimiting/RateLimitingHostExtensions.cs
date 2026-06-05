// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.ImageBuilder.RateLimiting;

public static class RateLimitingHostExtensions
{
    /// <summary>
    /// Registers a process-wide rate limiter for Azure Container Registry (ACR) requests and
    /// attaches the <see cref="AcrRateLimitingHandler"/> to every <c>HttpClient</c> created by
    /// <see cref="System.Net.Http.IHttpClientFactory"/>. Requests to non-ACR hosts pass through
    /// without consuming a permit.
    /// </summary>
    public static IServiceCollection AddAcrRateLimiting(this IServiceCollection services)
    {
        // The limiter is a singleton so its sliding window is shared across the process; the
        // handler is transient because IHttpClientFactory rebuilds handler chains periodically.
        services.AddSingleton(_ => new AcrRateLimiter());
        services.AddTransient<AcrRateLimitingHandler>();
        services.ConfigureHttpClientDefaults(httpClient =>
            httpClient.AddHttpMessageHandler<AcrRateLimitingHandler>()
        );

        return services;
    }
}
