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
            Func<Task<string>> getAccessToken,
            AsyncPolicy<HttpResponseMessage> policy)
        {
            HttpResponseMessage response = await policy
                .ExecuteAsync(async () =>
                {
                    HttpRequestMessage message = createMessage();

                    string accessToken = await getAccessToken();
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    return await httpClient.SendAsync(message);
                });

            response.EnsureSuccessStatusCode();

            return response;
        }
    }
}
