// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Acr;
using Newtonsoft.Json;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace Microsoft.DotNet.ImageBuilder
{
    public class AcrClient : IAcrClient, IDisposable
    {
        private const int MaxPagedResults = 500;
        private const string LinkUrlGroup = "LinkUrl";
        private const string RelationshipTypeGroup = "RelationshipType";
        private static readonly Regex linkHeaderRegex =
            new Regex($"<(?<{LinkUrlGroup}>.+)>;\\s*rel=\"(?<{RelationshipTypeGroup}>.+)\"");
        private static readonly string[] scopes = new string[]
        {
            "registry:catalog:*",
            "repository:*:metadata_read",
            "repository:*:delete",
            "repository:*:pull"
        };

        private readonly HttpClient httpClient;
        private readonly string acrName;
        private readonly ILoggerService loggerService;
        private readonly string baseUrl;
        private readonly string acrV1BaseUrl;
        private readonly string acrV2BaseUrl;
        private readonly AsyncLockedValue<string> acrRefreshToken;
        private readonly AsyncLockedValue<string> acrAccessToken;
        private readonly SemaphoreSlim sharedSemaphore = new SemaphoreSlim(1);
        private readonly string tenant;
        private readonly string aadAccessToken;

        private AcrClient(HttpClient httpClient, string acrName, string tenant, string aadAccessToken, ILoggerService loggerService)
        {
            this.httpClient = httpClient;
            this.acrName = acrName;
            this.tenant = tenant;
            this.aadAccessToken = aadAccessToken;
            this.loggerService = loggerService;
            this.baseUrl = $"https://{acrName}";
            this.acrV1BaseUrl = $"{baseUrl}/acr/v1";
            this.acrV2BaseUrl = $"{baseUrl}/v2";

            this.acrRefreshToken = new AsyncLockedValue<string>(semaphore: this.sharedSemaphore);
            this.acrAccessToken = new AsyncLockedValue<string>(semaphore: this.sharedSemaphore);
        }

        public async Task<Catalog> GetCatalogAsync()
        {
            Catalog result = null;
            await GetPagedResponseAsync<Catalog>(
                $"{this.acrV1BaseUrl}/_catalog?n={MaxPagedResults}",
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

            return result;
        }

        public Task<Repository> GetRepositoryAsync(string name)
        {
            return SendGetRequestAsync<Repository>($"{this.acrV1BaseUrl}/{name}");
        }

        public async Task<DeleteRepositoryResponse> DeleteRepositoryAsync(string name)
        {
            HttpResponseMessage response = await SendRequestAsync(
                () => new HttpRequestMessage(HttpMethod.Delete, $"{this.acrV1BaseUrl}/{name}"));
            return JsonConvert.DeserializeObject<DeleteRepositoryResponse>(await response.Content.ReadAsStringAsync());
        }

        public async Task<RepositoryManifests> GetRepositoryManifestsAsync(string repositoryName)
        {
            RepositoryManifests result = null;
            await GetPagedResponseAsync<RepositoryManifests>(
                $"{this.acrV1BaseUrl}/{repositoryName}/_manifests?n={MaxPagedResults}",
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

            return result;
        }

        public Task DeleteManifestAsync(string repositoryName, string digest)
        {
            return SendRequestAsync(
                () => new HttpRequestMessage(
                    HttpMethod.Delete, $"{this.acrV2BaseUrl}/{repositoryName}/manifests/{digest}"));
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

                if (response.Headers.TryGetValues("Link", out IEnumerable<string> linkValues))
                {
                    Match nextLinkMatch = linkValues
                        .Select(linkValue => linkHeaderRegex.Match(linkValue))
                        .FirstOrDefault(match => match.Success && match.Groups[RelationshipTypeGroup].Value == "next");

                    if (nextLinkMatch == null)
                    {
                        throw new InvalidOperationException(
                            $"Unable to parse link header '{String.Join(", ", linkValues.ToArray())}'");
                    }

                    currentUrl = $"{baseUrl}{nextLinkMatch.Groups[LinkUrlGroup].Value}";
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

        private async Task<HttpResponseMessage> SendRequestAsync(Func<HttpRequestMessage> createMessage)
        {
            var waitPolicy = Policy
                .HandleResult<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.TooManyRequests)
                .Or<TaskCanceledException>(exception =>
                    exception.InnerException is IOException ioException &&
                    ioException.InnerException is SocketException)
                .WaitAndRetryAsync(
                    Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(10), RetryHelper.MaxRetries),
                    RetryHelper.GetOnRetryDelegate<HttpResponseMessage>(RetryHelper.MaxRetries, loggerService));

            var unauthorizedPolicy = Policy
                .HandleResult<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.Unauthorized)
                .RetryAsync(1, async (result, retryCount, context) =>
                {
                    await GetAcrAccessTokenAsync(refresh: true);
                });

            HttpResponseMessage response = await waitPolicy
                .WrapAsync(unauthorizedPolicy)
                .ExecuteAsync(async () =>
                {
                    HttpRequestMessage message = createMessage();

                    string acrAccessToken = await GetAcrAccessTokenAsync();
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", acrAccessToken);
                    return await httpClient.SendAsync(message);
                });

            response.EnsureSuccessStatusCode();

            return response;
        }

        private Task<string> GetAcrRefreshTokenAsync()
        {
            return this.acrRefreshToken.GetValueAsync(async () =>
            {
                StringContent oauthExchangeBody = new StringContent(
                    $"grant_type=access_token&service={acrName}&tenant={tenant}&access_token={aadAccessToken}",
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded");

                HttpResponseMessage tokenExchangeResponse = await httpClient.PostAsync(
                    $"https://{acrName}/oauth2/exchange", oauthExchangeBody);
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
                string scopesArgs = String.Join('&', scopes
                    .Select(scope => $"scope={scope}")
                    .ToArray());
                StringContent oauthTokenBody = new StringContent(
                    $"grant_type=refresh_token&service={acrName}&{scopesArgs}&refresh_token={refreshToken}",
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded");
                HttpResponseMessage tokenResponse = await httpClient.PostAsync($"https://{acrName}/oauth2/token", oauthTokenBody);
                tokenResponse.EnsureSuccessStatusCode();
                OAuthTokenResult acrAccessTokenResult = JsonConvert.DeserializeObject<OAuthTokenResult>(
                    await tokenResponse.Content.ReadAsStringAsync());
                return acrAccessTokenResult.AccessToken;
            }

            return refresh ?
                await this.acrAccessToken.ResetValueAsync(valueInitializer) :
                await this.acrAccessToken.GetValueAsync(valueInitializer);
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
            this.sharedSemaphore.Dispose();
        }
    }
}
