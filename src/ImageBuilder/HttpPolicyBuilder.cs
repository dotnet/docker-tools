// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Polly;
using Polly.Contrib.WaitAndRetry;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public class HttpPolicyBuilder
    {
        private readonly List<AsyncPolicy<HttpResponseMessage>> _policies = new List<AsyncPolicy<HttpResponseMessage>>();

        public static HttpPolicyBuilder Create()
        {
            return new HttpPolicyBuilder();
        }

        public HttpPolicyBuilder WithMeteredRetryPolicy(ILoggerService loggerService)
        {
            _policies.Add(Policy
                .HandleResult<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.TooManyRequests)
                .Or<TaskCanceledException>(exception =>
                    exception.InnerException is IOException ioException &&
                    ioException.InnerException is SocketException)
                .WaitAndRetryAsync(
                    Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(10), RetryHelper.MaxRetries),
                    RetryHelper.GetOnRetryDelegate<HttpResponseMessage>(RetryHelper.MaxRetries, loggerService)));
            return this;
        }

        public HttpPolicyBuilder WithRefreshAccessTokenPolicy(Func<Task> refreshAccessToken, ILoggerService loggerService)
        {
            _policies.Add(Policy
                .HandleResult<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.Unauthorized)
                .RetryAsync(1, async (result, retryCount, context) =>
                {
                    loggerService.WriteMessage(
                        $"Unauthorized status code returned for '{result.Result.RequestMessage?.RequestUri}'. Refreshing access token and retrying.");
                    await refreshAccessToken();
                }));
            return this;
        }

        public HttpPolicyBuilder WithNotFoundRetryPolicy(TimeSpan timeout, TimeSpan retryFrequency, ILoggerService loggerService)
        {
            IEnumerable<TimeSpan> sleepDurations = Enumerable
                .Repeat(retryFrequency.TotalSeconds, (int)(timeout.TotalSeconds / retryFrequency.TotalSeconds))
                .Select(val => TimeSpan.FromSeconds(val));
            _policies.Add(Policy.HandleResult<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.NotFound)
                .WaitAndRetryAsync(sleepDurations, (result, duration) =>
                {
                    loggerService.WriteMessage(
                        $"NotFound status code returned for '{result.Result.RequestMessage?.RequestUri}'. Retrying in {retryFrequency.TotalSeconds} seconds.");
                }));
            return this;
        }

        public AsyncPolicy<HttpResponseMessage>? Build()
        {
            if (!_policies.Any())
            {
                return null;
            }

            AsyncPolicy<HttpResponseMessage> policy = _policies[0];
            for (int i = 1; i < _policies.Count; i++)
            {
                policy = policy.WrapAsync(_policies[i]);
            }

            return policy;
        }
    }
}
#nullable disable
