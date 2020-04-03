﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Acr;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder
{
    public class AcrClient : IAcrClient, IDisposable
    {
        private readonly HttpClient httpClient = new HttpClient();
        private readonly string acrV1BaseUrl;
        private readonly string acrV2BaseUrl;

        private AcrClient(HttpClient httpClient,string acrName)
        {
            this.httpClient = httpClient;
            this.acrV1BaseUrl = $"https://{acrName}/acr/v1";
            this.acrV2BaseUrl = $"https://{acrName}/v2";
        }

        public async Task<Catalog> GetCatalogAsync()
        {
            HttpResponseMessage response = await this.httpClient.GetAsync($"{this.acrV1BaseUrl}/_catalog?n={Int32.MaxValue}");
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<Catalog>(await response.Content.ReadAsStringAsync());
        }

        public async Task<Repository> GetRepositoryAsync(string name)
        {
            HttpResponseMessage response = await this.httpClient.GetAsync(
                $"{this.acrV1BaseUrl}/{name}");
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<Repository>(await response.Content.ReadAsStringAsync());
        }

        public async Task<DeleteRepositoryResponse> DeleteRepositoryAsync(string name)
        {
            HttpResponseMessage response = await this.httpClient.DeleteAsync(
                $"{this.acrV1BaseUrl}/{name}");
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<DeleteRepositoryResponse>(await response.Content.ReadAsStringAsync());
        }

        public async Task<RepositoryManifests> GetRepositoryManifests(string repositoryName)
        {
            HttpResponseMessage response = await this.httpClient.GetAsync(
                $"{this.acrV1BaseUrl}/{repositoryName}/_manifests?n={Int32.MaxValue}");
            response.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<RepositoryManifests>(await response.Content.ReadAsStringAsync());
        }

        public async Task DeleteManifestAsync(string repositoryName, string digest)
        {
            HttpResponseMessage response = await this.httpClient.DeleteAsync(
                $"{this.acrV2BaseUrl}/{repositoryName}/manifests/{digest}");
            response.EnsureSuccessStatusCode();
        }

        public static async Task<IAcrClient> CreateAsync(string acrName, string tenant, string username, string password)
        {
            string aadAccessToken = await GetAadAccessTokenAsync(tenant, username, password);
            
            HttpClient httpClient = new HttpClient();
            string acrRefreshToken = await GetAcrRefreshTokenAsync(httpClient, acrName, tenant, aadAccessToken);

            string accessToken = await GetAcrAccessTokenAsync(
                httpClient,
                acrName,
                acrRefreshToken,
                "registry:catalog:*",
                "repository:*:metadata_read",
                "repository:*:delete");

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            return new AcrClient(httpClient, acrName);
        }

        private static async Task<string> GetAadAccessTokenAsync(string tenant, string username, string password)
        {
            AuthenticationContext authContext = new AuthenticationContext($"https://login.microsoftonline.com/{tenant}");
            AuthenticationResult result = await authContext.AcquireTokenAsync(
                "https://management.azure.com", new ClientCredential(username, password));
            return result.AccessToken;
        }

        private static async Task<string> GetAcrRefreshTokenAsync(
            HttpClient httpClient, string acrName, string tenant, string aadAccessToken)
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
        }

        private static async Task<string> GetAcrAccessTokenAsync(
            HttpClient httpClient, string acrName, string refreshToken, params string[] scopes)
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

        public void Dispose()
        {
            this.httpClient.Dispose();
        }
    }
}
