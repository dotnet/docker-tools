// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Acr;
using Newtonsoft.Json;
using Polly;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public class AcrClient : IAcrClient, IDisposable
    {
        private const int MaxPagedResults = 500;
        private const string LinkUrlGroup = "LinkUrl";
        private const string RelationshipTypeGroup = "RelationshipType";
        private static readonly Regex s_linkHeaderRegex =
            new Regex($"<(?<{LinkUrlGroup}>.+)>;\\s*rel=\"(?<{RelationshipTypeGroup}>.+)\"");
        private static readonly string[] s_scopes = new string[]
        {
            "registry:catalog:*",
            "repository:*:metadata_read",
            "repository:*:delete",
            "repository:*:pull"
        };

        private readonly HttpClient _httpClient;
        private readonly string _acrName;
        private readonly string _baseUrl;
        private readonly string _acrV1BaseUrl;
        private readonly string _acrV2BaseUrl;
        private readonly AsyncLockedValue<string> _acrRefreshToken;
        private readonly AsyncLockedValue<string> _acrAccessToken;
        private readonly SemaphoreSlim _sharedSemaphore = new SemaphoreSlim(1);
        private readonly string _tenant;
        private readonly string _aadAccessToken;
        private readonly AsyncPolicy<HttpResponseMessage> _httpPolicy;

        private AcrClient(HttpClient httpClient, string acrName, string tenant, string aadAccessToken, ILoggerService loggerService)
        {
            _httpClient = httpClient;
            _acrName = acrName;
            _tenant = tenant;
            _aadAccessToken = aadAccessToken;
            _baseUrl = $"https://{acrName}";
            _acrV1BaseUrl = $"{_baseUrl}/acr/v1";
            _acrV2BaseUrl = $"{_baseUrl}/v2";

            _acrRefreshToken = new AsyncLockedValue<string>(semaphore: _sharedSemaphore);
            _acrAccessToken = new AsyncLockedValue<string>(semaphore: _sharedSemaphore);

            _httpPolicy = HttpPolicyBuilder.Create()
                .WithMeteredRetryPolicy(loggerService)
                .WithRefreshAccessTokenPolicy(GetAcrRefreshTokenAsync, loggerService)
                .Build() ?? throw new InvalidOperationException("Policy should not be null");
        }

        public async Task<Catalog> GetCatalogAsync()
        {
            Catalog? result = null;
            await GetPagedResponseAsync<Catalog>(
                $"{_acrV1BaseUrl}/_catalog?n={MaxPagedResults}",
                pagedCatalog =>
                {
                    if (result is null)
                    {
                        result = pagedCatalog;
                    }
                    else
                    {
                        result.RepositoryNames.AddRange(pagedCatalog.RepositoryNames);
                    }
                });

            return result ?? throw new InvalidOperationException("Catalog should not be null.");
        }

        public Task<Repository> GetRepositoryAsync(string name)
        {
            return SendGetRequestAsync<Repository>($"{_acrV1BaseUrl}/{name}");
        }

        public async Task<DeleteRepositoryResponse> DeleteRepositoryAsync(string name)
        {
            HttpResponseMessage response = await SendRequestAsync(
                () => new HttpRequestMessage(HttpMethod.Delete, $"{_acrV1BaseUrl}/{name}"));
            return JsonConvert.DeserializeObject<DeleteRepositoryResponse>(await response.Content.ReadAsStringAsync());
        }

        public async Task<RepositoryManifests> GetRepositoryManifestsAsync(string repositoryName)
        {
            RepositoryManifests? result = null;
            await GetPagedResponseAsync<RepositoryManifests>(
                $"{_acrV1BaseUrl}/{repositoryName}/_manifests?n={MaxPagedResults}",
                pagedRepoManifests =>
            {
                if (result is null)
                {
                    result = pagedRepoManifests;
                }
                else
                {
                    result.Manifests.AddRange(pagedRepoManifests.Manifests);
                }
            });

            return result ?? throw new InvalidOperationException("Catalog should not be null.");
        }

        public Task DeleteManifestAsync(string repositoryName, string digest)
        {
            return SendRequestAsync(
                () => new HttpRequestMessage(
                    HttpMethod.Delete, $"{_acrV2BaseUrl}/{repositoryName}/manifests/{digest}"));
        }

        public static async Task<IAcrClient> CreateAsync(string acrName, string tenant, string username, string password,
            ILoggerService loggerService, IHttpClientProvider httpClientProvider)
        {
            string aadAccessToken = await AuthHelper.GetAadAccessTokenAsync("https://management.azure.com", tenant, username, password);

            return new AcrClient(httpClientProvider.GetClient(), acrName, tenant, aadAccessToken, loggerService);
        }

        private async Task GetPagedResponseAsync<T>(string url, Action<T> onGetResults)
        {
            string currentUrl = url;
            while (true)
            {
                HttpResponseMessage response = await SendRequestAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, currentUrl));
                
                T results = JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());

                onGetResults(results);

                if (response.Headers.TryGetValues("Link", out IEnumerable<string>? linkValues))
                {
                    Match? nextLinkMatch = linkValues
                        .Select(linkValue => s_linkHeaderRegex.Match(linkValue))
                        .FirstOrDefault(match => match.Success && match.Groups[RelationshipTypeGroup].Value == "next");

                    if (nextLinkMatch == null)
                    {
                        throw new InvalidOperationException(
                            $"Unable to parse link header '{string.Join(", ", linkValues.ToArray())}'");
                    }

                    currentUrl = $"{_baseUrl}{nextLinkMatch.Groups[LinkUrlGroup].Value}";
                }
                else
                {
                    return;
                }
            }
        }

        private async Task<T> SendGetRequestAsync<T>(string url)
        {
            HttpResponseMessage response = await SendRequestAsync(
                () => new HttpRequestMessage(HttpMethod.Get, url));
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());
        }

        private Task<HttpResponseMessage> SendRequestAsync(Func<HttpRequestMessage> createMessage) =>
            _httpClient.SendRequestAsync(createMessage, () => GetAcrAccessTokenAsync(), _httpPolicy);

        private Task<string> GetAcrRefreshTokenAsync()
        {
            return _acrRefreshToken.GetValueAsync(async () =>
            {
                StringContent oauthExchangeBody = new StringContent(
                    $"grant_type=access_token&service={_acrName}&tenant={_tenant}&access_token={_aadAccessToken}",
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded");

                HttpResponseMessage tokenExchangeResponse = await _httpClient.PostAsync(
                    $"https://{_acrName}/oauth2/exchange", oauthExchangeBody);
                tokenExchangeResponse.EnsureSuccessStatusCode();
                OAuthExchangeResult acrRefreshTokenResult = JsonConvert.DeserializeObject<OAuthExchangeResult>(
                    await tokenExchangeResponse.Content.ReadAsStringAsync());
               return acrRefreshTokenResult.RefreshToken;
            });
        }

        private async Task<string> GetAcrAccessTokenAsync(bool refresh = false)
        {
            string refreshToken = await GetAcrRefreshTokenAsync();
            async Task<string> valueInitializer()
            {
                string scopesArgs = string.Join('&', s_scopes
                    .Select(scope => $"scope={scope}")
                    .ToArray());
                StringContent oauthTokenBody = new StringContent(
                    $"grant_type=refresh_token&service={_acrName}&{scopesArgs}&refresh_token={refreshToken}",
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded");
                HttpResponseMessage tokenResponse = await _httpClient.PostAsync($"https://{_acrName}/oauth2/token", oauthTokenBody);
                tokenResponse.EnsureSuccessStatusCode();
                OAuthTokenResult acrAccessTokenResult = JsonConvert.DeserializeObject<OAuthTokenResult>(
                    await tokenResponse.Content.ReadAsStringAsync());
                return acrAccessTokenResult.AccessToken;
            }

            return refresh ?
                await _acrAccessToken.ResetValueAsync(valueInitializer) :
                await _acrAccessToken.GetValueAsync(valueInitializer);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _sharedSemaphore.Dispose();
        }
    }
}
#nullable disable
