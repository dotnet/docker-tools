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
        private readonly Lazy<HttpClient> _httpClient;
        private readonly Lazy<RegistryHttpClient> _registryHttpClient;

        public HttpClientProvider(ILogger<HttpClientProvider> loggerService)
        {
            if (loggerService is null)
            {
                throw new ArgumentNullException(nameof(loggerService));
            }

            _httpClient = new Lazy<HttpClient>(() => new HttpClient(new LoggingHandler(loggerService)));
            _registryHttpClient = new Lazy<RegistryHttpClient>(() => new RegistryHttpClient(new LoggingHandler(loggerService)));
        }

        public HttpClient GetClient() => _httpClient.Value;

        public RegistryHttpClient GetRegistryClient() => _registryHttpClient.Value;

        private class LoggingHandler : MessageProcessingHandler
        {
            private readonly ILogger _loggerService;

            public LoggingHandler(ILogger loggerService)
            {
                _loggerService = loggerService;
                InnerHandler = new HttpClientHandler();
            }

            protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _loggerService.LogInformation($"Sending HTTP request: {request.RequestUri}");
                return request;
            }

            protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
            {
                return response;
            }
        }
    }
}
