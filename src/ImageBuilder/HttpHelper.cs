// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Polly;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class HttpHelper
    {
        public static async Task<HttpResponseMessage> SendRequestAsync(
            this HttpClient httpClient,
            Func<HttpRequestMessage> createMessage,
            Func<CancellationToken, Task<string>> getAccessToken,
            AsyncPolicy<HttpResponseMessage> policy,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await policy
                .ExecuteAsync(async ct =>
                {
                    HttpRequestMessage message = createMessage();

                    string accessToken = await getAccessToken(ct);
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    return await httpClient.SendAsync(message, ct);
                },
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return response;
        }
    }
}
