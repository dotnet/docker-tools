// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    internal class HttpClientProvider : IHttpClientProvider
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly Lazy<HttpClient> _httpClient;
        private readonly Lazy<RegistryHttpClient> _registryHttpClient;

        public HttpClientProvider(ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _loggerFactory = loggerFactory;
            _httpClient = new Lazy<HttpClient>(() => new HttpClient(new LoggingHandler(_loggerFactory)));
            _registryHttpClient = new Lazy<RegistryHttpClient>(() => new RegistryHttpClient(new LoggingHandler(_loggerFactory)));
        }

        public HttpClient GetClient() => _httpClient.Value;

        public RegistryHttpClient GetRegistryClient() => _registryHttpClient.Value;

        /// <summary>
        /// Logs HTTP request/response activity. Successful responses are logged at Debug level.
        /// Failures (timeouts, cancellations, transport errors) are logged at Warning level
        /// with elapsed time to aid diagnosis of hanging requests.
        /// </summary>
        private class LoggingHandler : DelegatingHandler
        {
            private readonly ILogger<LoggingHandler> _logger;

            public LoggingHandler(ILoggerFactory loggerFactory)
            {
                ArgumentNullException.ThrowIfNull(loggerFactory);
                _logger = loggerFactory.CreateLogger<LoggingHandler>();
                InnerHandler = new HttpClientHandler();
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                long startTime = Stopwatch.GetTimestamp();

                try
                {
                    HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                    _logger.LogDebug("HTTP {StatusCode} {Method} {RequestUri} in {Elapsed}",
                        (int)response.StatusCode, request.Method, request.RequestUri, Stopwatch.GetElapsedTime(startTime));
                    return response;
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "HTTP timeout {Method} {RequestUri} after {Elapsed}",
                        request.Method, request.RequestUri, Stopwatch.GetElapsedTime(startTime));
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "HTTP failure {Method} {RequestUri} after {Elapsed}",
                        request.Method, request.RequestUri, Stopwatch.GetElapsedTime(startTime));
                    throw;
                }
            }
        }
    }
}
