#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers
{
    /// <summary>
    /// A custom <see cref="HttpMessageHandler"/> that bypass networks calls and returns back custom responses.
    /// </summary>
    public class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly IDictionary<string, HttpResponseMessage> responses;

        public TestHttpMessageHandler(IDictionary<string, HttpResponseMessage> responses)
        {
            this.responses = responses ?? throw new ArgumentNullException(nameof(responses));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.responses[request.RequestUri.ToString()]);
        }
    }
}
