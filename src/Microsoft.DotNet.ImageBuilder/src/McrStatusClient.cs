// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;
using Newtonsoft.Json;
using Polly;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IMcrStatusClient))]
    public class McrStatusClient : IMcrStatusClient
    {
        private const string McrStatusResource = "api://c00053c3-a979-4ee6-b94e-941881e62d8e";
        // https://msazure.visualstudio.com/MicrosoftContainerRegistry/_git/docs?path=/status/status_v2.yaml
        private const string BaseUri = "https://status.mscr.io/api/onboardingstatus/v2";
        private readonly HttpClient _httpClient;
        private readonly AsyncLockedValue<string> _accessToken = new AsyncLockedValue<string>();
        private readonly AsyncPolicy<HttpResponseMessage> _httpPolicy;

        [ImportingConstructor]
        public McrStatusClient(IHttpClientProvider httpClientProvider, ILoggerService loggerService)
        {
            if (loggerService is null)
            {
                throw new ArgumentNullException(nameof(loggerService));
            }

            if (httpClientProvider is null)
            {
                throw new ArgumentNullException(nameof(httpClientProvider));
            }

            _httpClient = httpClientProvider.GetClient();
            _httpPolicy = HttpPolicyBuilder.Create()
                .WithMeteredRetryPolicy(loggerService)
                .WithRefreshAccessTokenPolicy(RefreshAccessTokenAsync, loggerService)
                .WithNotFoundRetryPolicy(TimeSpan.FromHours(1), TimeSpan.FromSeconds(10), loggerService)
                .Build() ?? throw new InvalidOperationException("Policy should not be null");
        }

        public Task<ImageResult> GetImageResultAsync(string imageDigest)
        {
            string uri = $"{BaseUri}/images/{imageDigest}";
            return SendRequestAsync<ImageResult>(() => new HttpRequestMessage(HttpMethod.Get, uri));
        }

        public Task<ImageResultDetailed> GetImageResultDetailedAsync(string imageDigest, string onboardingRequestId)
        {
            string uri = $"{BaseUri}/images/{imageDigest}/{onboardingRequestId}";
            return SendRequestAsync<ImageResultDetailed>(() => new HttpRequestMessage(HttpMethod.Get, uri));
        }

        public Task<CommitResult> GetCommitResultAsync(string commitDigest)
        {
            string uri = $"{BaseUri}/commits/{commitDigest}";
            return SendRequestAsync<CommitResult>(() => new HttpRequestMessage(HttpMethod.Get, uri));
        }

        public Task<CommitResultDetailed> GetCommitResultDetailedAsync(string commitDigest, string onboardingRequestId)
        {
            string uri = $"{BaseUri}/commits/{commitDigest}/{onboardingRequestId}";
            return SendRequestAsync<CommitResultDetailed>(() => new HttpRequestMessage(HttpMethod.Get, uri));
        }

        private async Task<T> SendRequestAsync<T>(Func<HttpRequestMessage> message)
        {
            HttpResponseMessage response = await _httpClient.SendRequestAsync(message, GetAccessTokenAsync, _httpPolicy);
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());
        }

        private Task<string> GetAccessTokenAsync() =>
            _accessToken.GetValueAsync(
                () => AuthHelper.GetDefaultAccessTokenAsync(McrStatusResource));

        private Task RefreshAccessTokenAsync() =>
            _accessToken.ResetValueAsync(
                () => AuthHelper.GetDefaultAccessTokenAsync(McrStatusResource));
    }
}
