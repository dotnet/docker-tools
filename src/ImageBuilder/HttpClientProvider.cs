// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Threading;

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

        private class LoggingHandler : MessageProcessingHandler
        {
            private readonly ILogger<LoggingHandler> _logger;

            public LoggingHandler(ILoggerFactory loggerFactory)
            {
                ArgumentNullException.ThrowIfNull(loggerFactory);
                _logger = loggerFactory.CreateLogger<LoggingHandler>();
                InnerHandler = new HttpClientHandler();
            }

            protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _logger.LogInformation($"Sending HTTP request: {request.RequestUri}");
                return request;
            }

            protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
            {
                return response;
            }
        }
    }
}
