// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Net.Http;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IHttpClientProvider))]
    internal class HttpClientProvider : IHttpClientProvider
    {
        private readonly Lazy<HttpClient> httpClient = new Lazy<HttpClient>(() => new HttpClient());

        public HttpClient GetClient()
        {
            return httpClient.Value;
        }
    }
}
