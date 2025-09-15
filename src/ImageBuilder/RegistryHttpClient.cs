// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
/// <summary>
/// A specialized type of <see cref="HttpClient"/> that is preconfigured to handle the OAuth flow of container registries.
/// </summary>
public class RegistryHttpClient : HttpClient
{
    public RegistryHttpClient(HttpMessageHandler httpMessageHandler)
        : base(CreateHttpHandlerPipeline(httpMessageHandler))
    {

    }

    private static HttpMessageHandler CreateHttpHandlerPipeline(HttpMessageHandler rootHandler) =>
        new RegistryOAuthDelegatingHandler
        {
            InnerHandler = rootHandler
        };

    /// <summary>
    /// Handles the OAuth handshaking with container registries.
    /// </summary>
    private class RegistryOAuthDelegatingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            // If the initial request is unauthorized, respond to the OAuth challenge by getting an access token and sending the request
            // again with that token.
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                request = await GetAuthenticatedRequestAsync(response, request, cancellationToken);
                response = await base.SendAsync(request, cancellationToken);
            }

            return response;
        }

        private async Task<HttpRequestMessage> GetAuthenticatedRequestAsync(
            HttpResponseMessage response, HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            // Respond to an authorized request by getting an access token.
            OAuthToken authToken = await GetOAuthTokenAsync(response, request, cancellationToken);

            // Set the token on the Authorization header of the request to be resent.
            request.Headers.Authorization = new AuthenticationHeaderValue(
                HttpBearerChallenge.Bearer, authToken.AccessToken ?? authToken.Token);
            return request;
        }

        private async Task<OAuthToken> GetOAuthTokenAsync(
            HttpResponseMessage response, HttpRequestMessage unauthorizedRequest, CancellationToken cancellationToken = default)
        {
            // The unauthorized response will container a bearer header with the OAuth challenge, indicating the parameters
            // for retrieving an access token.

            AuthenticationHeaderValue? bearerHeader = response.Headers.WwwAuthenticate
                .AsEnumerable()
                .FirstOrDefault(header => header.Scheme == HttpBearerChallenge.Bearer);

            if (bearerHeader is null)
            {
                throw new AuthenticationException(
                    $"Bearer header not contained in unauthorized response from {response.RequestMessage?.RequestUri}");
            }

            HttpBearerChallenge challenge = HttpBearerChallenge.Parse(bearerHeader.Parameter);

            // Construct the URL to retrieve the access token based on the parameters of the bearer challenge
            // See https://docs.docker.com/registry/spec/auth/jwt/
            Uri authenticateUri = new($"{challenge.Realm}?service={challenge.Service}&scope={challenge.Scope}");
            HttpRequestMessage authenticateRequest = new(HttpMethod.Get, authenticateUri);
            authenticateRequest.Headers.Authorization = unauthorizedRequest.Headers.Authorization;

            // Send the request to get the access token
            response = await base.SendAsync(authenticateRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            string tokenContent = await response.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                return JsonConvert.DeserializeObject<OAuthToken>(tokenContent) ??
                    throw new JsonSerializationException(
                        $"Unable to deserialize the response.{Environment.NewLine}{Environment.NewLine}Content:{Environment.NewLine}{tokenContent}"); ;
            }
            catch (JsonException e)
            {
                throw new JsonSerializationException(
                    $"Unable to deserialize the response.{Environment.NewLine}{Environment.NewLine}Content:{Environment.NewLine}{tokenContent}", e);
            }
        }

        private class OAuthToken
        {
            [JsonProperty("token")]
            public string? Token { get; set; }

            [JsonProperty("access_token")]
            public string? AccessToken { get; set; }

            [JsonProperty("expires_in")]
            public int? ExpiresIn { get; set; }

            [JsonProperty("issued_at")]
            public DateTime? IssuedAt { get; set; }
        }

        private class HttpBearerChallenge
        {
            private const string ServiceParameter = "service";
            private const string ScopeParameter = "scope";
            private const string RealmParameter = "realm";
            public const string Bearer = "Bearer";

            private static readonly Regex s_bearerRegex = new(
                $"(realm=\"(?<{RealmParameter}>.+?)\"|service=\"(?<{ServiceParameter}>.+?)\"|scope=\"(?<{ScopeParameter}>.+?)\")");

            public string Realm { get; }
            public string Service { get; }
            public string Scope { get; }

            public HttpBearerChallenge(string realm, string service, string scope)
            {
                Realm = realm;
                Service = service;
                Scope = scope;
            }

            public static HttpBearerChallenge Parse(string? challenge)
            {
                if (challenge is null || !ValidateChallenge(challenge))
                {
                    throw new ArgumentException($"Unable to parse HTTP bearer from '{challenge}'.", challenge);
                }

                MatchCollection matches = s_bearerRegex.Matches(challenge);

                string? realm = null;
                string? service = null;
                string? scope = null;

                foreach (Match match in matches)
                {
                    realm ??= GetGroupValue(match, RealmParameter);
                    service ??= GetGroupValue(match, ServiceParameter);
                    scope ??= GetGroupValue(match, ScopeParameter);
                }

                if (realm is null)
                {
                    throw new ArgumentException($"Unable to parse realm from '{challenge}'.", challenge);
                }

                if (service is null)
                {
                    throw new ArgumentException($"Unable to parse service from '{challenge}'.", challenge);
                }

                if (scope is null)
                {
                    throw new ArgumentException($"Unable to parse scope from '{challenge}'.", challenge);
                }

                return new HttpBearerChallenge(realm, service, scope);
            }

            private static string? GetGroupValue(Match match, string groupName)
            {
                Group group = match.Groups[groupName];
                return group.Success ? group.Value : null;
            }

            private static bool ValidateChallenge(string? challenge) =>
                !string.IsNullOrEmpty(challenge) && s_bearerRegex.IsMatch(challenge);
        }
    }
}
#nullable disable
