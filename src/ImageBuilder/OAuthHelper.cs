// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

public static class OAuthHelper
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static async Task<string> GetRefreshTokenAsync(HttpClient httpClient, string acrName, Guid tenant, string eidToken)
    {
        StringContent requestContent = new(
            $"grant_type=access_token&service={acrName}&tenant={tenant}&access_token={eidToken}",
            Encoding.UTF8,
            "application/x-www-form-urlencoded");

        HttpResponseMessage tokenExchangeResponse = await httpClient.PostAsync($"https://{acrName}/oauth2/exchange", requestContent);
        tokenExchangeResponse.EnsureSuccessStatusCode();

        OAuthExchangeResult result = await tokenExchangeResponse.Content.ReadFromJsonAsync<OAuthExchangeResult>(s_jsonOptions)
                ?? throw new Exception($"Got null when serializing {nameof(OAuthExchangeResult)}.");

        string? token = result.RefreshToken;
        if (string.IsNullOrEmpty(token))
        {
            throw new Exception($"{nameof(OAuthExchangeResult)} contained null or empty Refresh Token.");
        }

        return token;
    }

    private record OAuthExchangeResult(string RefreshToken);
}
