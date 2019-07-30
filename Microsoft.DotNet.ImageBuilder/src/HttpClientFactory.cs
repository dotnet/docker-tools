// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using System.Net.Http;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IHttpClientFactory))]
    internal class HttpClientFactory : IHttpClientFactory
    {
        public HttpClient GetClient()
        {
            return new HttpClient();
        }
    }
}
