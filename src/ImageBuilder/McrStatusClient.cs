// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;
using Newtonsoft.Json;
using Polly;

namespace Microsoft.DotNet.ImageBuilder
{
    public class McrStatusClient : IMcrStatusClient
    {
        // https://msazure.visualstudio.com/MicrosoftContainerRegistry/_git/docs?path=/status/status_v2.yaml
        private const string BaseUri = "https://status.mscr.io/api/onboardingstatus/v2";
        private readonly HttpClient _httpClient;
        private readonly AsyncLockedValue<string> _accessToken = new AsyncLockedValue<string>();
        private readonly ILogger<McrStatusClient> _logger;
        private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;
        private readonly IServiceConnection _serviceConnection;

        public McrStatusClient(
            IHttpClientFactory httpClientFactory,
            ILogger<McrStatusClient> logger,
            IAzureTokenCredentialProvider tokenCredentialProvider,
            IServiceConnection serviceConnection)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            // The HttpClient created here inherits the default resilience handler configured in
            // ImageBuilder.cs, which already retries transient failures (5xx, 408, 429) with backoff.
            // Only the policies that the default pipeline can't express are added here: refreshing the
            // access token on 401 and long-polling on 404 while MCR onboarding completes.
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _tokenCredentialProvider = tokenCredentialProvider;
            _serviceConnection = serviceConnection;
        }

        public Task<ImageResult> GetImageResultAsync(string imageDigest, CancellationToken cancellationToken)
        {
            string uri = $"{BaseUri}/images/{imageDigest}";
            return SendRequestAsync<ImageResult>(() => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
        }

        public Task<ImageResultDetailed> GetImageResultDetailedAsync(string imageDigest, string onboardingRequestId, CancellationToken cancellationToken)
        {
            string uri = $"{BaseUri}/images/{imageDigest}/{onboardingRequestId}";
            return SendRequestAsync<ImageResultDetailed>(() => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
        }

        public Task<CommitResult> GetCommitResultAsync(string commitDigest, CancellationToken cancellationToken)
        {
            string uri = $"{BaseUri}/commits/{commitDigest}";
            return SendRequestAsync<CommitResult>(() => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
        }

        public Task<CommitResultDetailed> GetCommitResultDetailedAsync(string commitDigest, string onboardingRequestId, CancellationToken cancellationToken)
        {
            string uri = $"{BaseUri}/commits/{commitDigest}/{onboardingRequestId}";
            return SendRequestAsync<CommitResultDetailed>(() => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
        }

        private async Task<T> SendRequestAsync<T>(Func<HttpRequestMessage> message, CancellationToken cancellationToken)
        {
            AsyncPolicy<HttpResponseMessage> httpPolicy = HttpPolicyBuilder.Create()
                .WithRefreshAccessTokenPolicy(
                    () => RefreshAccessTokenAsync(cancellationToken), _logger)
                .WithNotFoundRetryPolicy(
                    TimeSpan.FromHours(1), TimeSpan.FromSeconds(10), _logger)
                .Build() ?? throw new InvalidOperationException("Policy should not be null");

            HttpResponseMessage response = await _httpClient.SendRequestAsync(message, GetAccessTokenAsync, httpPolicy, cancellationToken);
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync(cancellationToken))
                ?? throw new InvalidOperationException("Failed to deserialize response from MCR Status API.");
        }

        private Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            _accessToken.GetValueAsync(
                async ct =>
                    (await _tokenCredentialProvider.GetTokenAsync(_serviceConnection, ct, AzureScopes.McrStatusApi)).Token,
                cancellationToken);

        private Task RefreshAccessTokenAsync(CancellationToken cancellationToken) =>
            _accessToken.ResetValueAsync(
                cancellationToken,
                async ct =>
                    (await _tokenCredentialProvider.GetTokenAsync(_serviceConnection, ct, AzureScopes.McrStatusApi)).Token);
    }
}
