// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IAcrClientFactory))]
    public class AcrClientFactory : IAcrClientFactory
    {
        private readonly ILoggerService _loggerService;
        private readonly IHttpClientProvider _httpClientProvider;

        [ImportingConstructor]
        public AcrClientFactory(ILoggerService loggerService, IHttpClientProvider httpClientProvider)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _httpClientProvider = httpClientProvider;
        }

        public Task<IAcrClient> CreateAsync(string acrName, string tenant, string username, string password)
        {
            return AcrClient.CreateAsync(acrName, tenant, username, password, _loggerService, _httpClientProvider);
        }
    }
}
