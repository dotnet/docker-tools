// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IMcrStatusClientFactory))]
    public class McrStatusClientFactory : IMcrStatusClientFactory
    {
        private readonly IHttpClientProvider _httpClientProvider;
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public McrStatusClientFactory(IHttpClientProvider httpClientProvider, ILoggerService loggerService)
        {
            _httpClientProvider = httpClientProvider ?? throw new ArgumentNullException(nameof(httpClientProvider));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public IMcrStatusClient Create(string tenant, string clientId, string clientSecret) =>
            new McrStatusClient(_httpClientProvider.GetClient(), tenant, clientId, clientSecret, _loggerService);
    }
}
