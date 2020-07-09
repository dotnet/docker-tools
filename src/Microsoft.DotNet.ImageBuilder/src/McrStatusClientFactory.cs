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
        private readonly IHttpClientProvider httpClientProvider;
        private readonly ILoggerService loggerService;

        [ImportingConstructor]
        public McrStatusClientFactory(IHttpClientProvider httpClientProvider, ILoggerService loggerService)
        {
            this.httpClientProvider = httpClientProvider ?? throw new ArgumentNullException(nameof(httpClientProvider));
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public IMcrStatusClient Create(string tenant, string clientId, string clientSecret) =>
            new McrStatusClient(this.httpClientProvider.GetClient(), tenant, clientId, clientSecret, this.loggerService);
    }
}
